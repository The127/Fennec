using System.IdentityModel.Tokens.Jwt;
using Fennec.Api.FederationClient;
using Fennec.Api.Models;
using Fennec.Api.Services;
using Fennec.Api.Settings;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fennec.Api.Hubs;

public class MessageHub(
    VoiceStateService voiceState,
    PresenceService presenceService,
    GlobalPresenceService globalPresenceService,
    FederatedPresenceCache federatedPresenceCache,
    IFederationClient federationClient,
    IOptions<FennecSettings> fennecOptions,
    ILogger<MessageHub> logger,
    FennecDbContext dbContext,
    IServiceScopeFactory scopeFactory
) : Hub
{
    private string LocalInstanceUrl => fennecOptions.Value.IssuerUrl;

    public async Task SubscribeToChannel(Guid serverId, Guid channelId)
    {
        var groupName = $"{serverId}-{channelId}";
        logger.LogInformation("SignalR: Connection {ConnectionId} subscribing to group {Group}", Context.ConnectionId, groupName);
        // TODO: check if user is on the server and has access to the channel
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task UnsubscribeFromChannel(Guid serverId, Guid channelId)
    {
        var groupName = $"{serverId}-{channelId}";
        logger.LogInformation("SignalR: Connection {ConnectionId} unsubscribing from group {Group}", Context.ConnectionId, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SubscribeToServer(Guid serverId)
    {
        var groupName = ServerGroup(serverId);
        logger.LogInformation("SignalR: Connection {ConnectionId} subscribing to server group {Group}", Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        try
        {
            var (userId, username, callerInstanceUrl) = GetCallerIdentity();
            var isNew = presenceService.AddUser(serverId, userId, username, callerInstanceUrl, Context.ConnectionId);
            logger.LogInformation("Presence: AddUser server={ServerId} user={Username} userId={UserId} instanceUrl={InstanceUrl} isNew={IsNew}",
                serverId, username, userId, callerInstanceUrl, isNew);
            if (isNew)
            {
                await Clients.OthersInGroup(groupName).SendAsync("UserOnline", serverId, userId, username, callerInstanceUrl);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to track presence for connection {ConnectionId}", Context.ConnectionId);
        }
    }

    public async Task UnsubscribeFromServer(Guid serverId)
    {
        var groupName = ServerGroup(serverId);
        logger.LogInformation("SignalR: Connection {ConnectionId} unsubscribing from server group {Group}", Context.ConnectionId, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task<List<ServerPresenceEntryDto>> GetServerPresence(Guid serverId)
    {
        var online = presenceService.GetOnlineUsers(serverId);

        // Include remote members whose presence was pushed via federation
        var remoteMembers = await dbContext.Set<ServerMember>()
            .Where(sm => sm.ServerId == serverId)
            .Include(sm => sm.KnownUser)
            .Where(sm => !sm.KnownUser.IsDeleted)
            .Where(sm => sm.KnownUser.InstanceUrl != null && sm.KnownUser.InstanceUrl != LocalInstanceUrl)
            .ToListAsync();

        foreach (var member in remoteMembers)
        {
            if (federatedPresenceCache.IsOnline(member.KnownUser.RemoteId, member.KnownUser.InstanceUrl!)
                && online.All(e => e.UserId != member.KnownUser.RemoteId))
            {
                online.Add(new PresenceService.PresenceEntry(
                    member.KnownUser.RemoteId,
                    member.KnownUser.Name,
                    member.KnownUser.InstanceUrl,
                    ""));
            }
        }

        logger.LogInformation("Presence: GetServerPresence server={ServerId} count={Count} users={Users}",
            serverId, online.Count, string.Join(", ", online.Select(e => $"{e.Username}({e.UserId})")));
        return online
            .Select(e => new ServerPresenceEntryDto { UserId = e.UserId, Username = e.Username, InstanceUrl = e.InstanceUrl })
            .ToList();
    }

    public async Task<Dictionary<Guid, List<VoiceParticipantDto>>> GetServerVoiceState(Guid serverId, string instanceUrl)
    {
        if (IsRemote(instanceUrl))
        {
            try
            {
                return await federationClient.For(instanceUrl).Voice.GetVoiceStateAsync(serverId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get remote voice state for server={ServerId}", serverId);
                return new();
            }
        }

        return voiceState.GetServerVoiceState(serverId);
    }

    // --- Voice ---

    public async Task<List<VoiceParticipantDto>> JoinVoiceChannel(Guid serverId, Guid channelId, string instanceUrl)
    {
        try
        {
            var (userId, username, callerInstanceUrl) = GetCallerIdentity();

            if (IsRemote(instanceUrl))
            {
                // Forward to hosting instance via federation
                var response = await federationClient.For(instanceUrl).Voice.JoinAsync(new FederationVoiceJoinRequestDto
                {
                    ServerId = serverId,
                    ChannelId = channelId,
                    UserId = userId,
                    Username = username,
                    CallerInstanceUrl = callerInstanceUrl ?? LocalInstanceUrl,
                });

                // Track locally for disconnect cleanup
                voiceState.AddParticipant(serverId, channelId, userId, username, callerInstanceUrl ?? LocalInstanceUrl, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, VoiceGroup(serverId, channelId));
                await Groups.AddToGroupAsync(Context.ConnectionId, ServerGroup(serverId));

                return response.Participants;
            }

            // Local server — process normally
            var voiceGroup = VoiceGroup(serverId, channelId);
            await Groups.AddToGroupAsync(Context.ConnectionId, voiceGroup);
            var participants = voiceState.AddParticipant(serverId, channelId, userId, username, callerInstanceUrl, Context.ConnectionId);

            var participantDto = new VoiceParticipantDto { UserId = userId, Username = username, InstanceUrl = callerInstanceUrl };
            await Clients.OthersInGroup(voiceGroup).SendAsync("VoiceParticipantJoined", serverId, channelId, participantDto);
            await Clients.Group(ServerGroup(serverId)).SendAsync("VoiceParticipantJoined", serverId, channelId, participantDto);

            // Notify remote instances that have participants in this channel
            await NotifyRemoteInstancesParticipantJoined(serverId, channelId, participantDto);

            return participants;
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "JoinVoiceChannel failed for server={ServerId} channel={ChannelId}", serverId, channelId);
            throw new HubException($"Failed to join voice channel: {ex.Message}");
        }
    }

    public async Task LeaveVoiceChannel(Guid serverId, Guid channelId, string instanceUrl)
    {
        var (userId, _, _) = GetCallerIdentity();
        var voiceGroup = VoiceGroup(serverId, channelId);

        voiceState.RemoveParticipant(serverId, channelId, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, voiceGroup);

        if (IsRemote(instanceUrl))
        {
            // Forward leave to hosting instance
            try
            {
                await federationClient.For(instanceUrl).Voice.LeaveAsync(new FederationVoiceLeaveRequestDto
                {
                    ServerId = serverId,
                    ChannelId = channelId,
                    UserId = userId,
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to forward voice leave to remote instance {InstanceUrl}", instanceUrl);
            }
        }
        else
        {
            // Local server — broadcast and notify remote instances
            await Clients.OthersInGroup(voiceGroup).SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
            await Clients.Group(ServerGroup(serverId)).SendAsync("VoiceParticipantLeft", serverId, channelId, userId);

            await NotifyRemoteInstancesParticipantLeft(serverId, channelId, userId);
        }
    }

    public async Task SendSdpOffer(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        var (userId, _, _) = GetCallerIdentity();
        var connectionId = voiceState.GetConnectionId(serverId, channelId, targetUserId);
        if (connectionId is null) return;
        await Clients.Client(connectionId)
            .SendAsync("ReceiveSdpOffer", serverId, channelId, userId, sdp);
    }

    public async Task SendSdpAnswer(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        var (userId, _, _) = GetCallerIdentity();
        var connectionId = voiceState.GetConnectionId(serverId, channelId, targetUserId);
        if (connectionId is null) return;
        await Clients.Client(connectionId)
            .SendAsync("ReceiveSdpAnswer", serverId, channelId, userId, sdp);
    }

    public async Task SendIceCandidate(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        var (userId, _, _) = GetCallerIdentity();
        var connectionId = voiceState.GetConnectionId(serverId, channelId, targetUserId);
        if (connectionId is null) return;
        await Clients.Client(connectionId)
            .SendAsync("ReceiveIceCandidate", serverId, channelId, userId, candidate, sdpMid, sdpMLineIndex);
    }

    public async Task SetMuteState(Guid serverId, Guid channelId, bool isMuted)
    {
        var (userId, _, _) = GetCallerIdentity();
        await Clients.OthersInGroup(VoiceGroup(serverId, channelId))
            .SendAsync("VoiceMuteStateChanged", serverId, channelId, userId, isMuted);
        await Clients.OthersInGroup(ServerGroup(serverId))
            .SendAsync("VoiceMuteStateChanged", serverId, channelId, userId, isMuted);
    }

    public async Task SetDeafenState(Guid serverId, Guid channelId, bool isDeafened)
    {
        var (userId, _, _) = GetCallerIdentity();
        await Clients.OthersInGroup(VoiceGroup(serverId, channelId))
            .SendAsync("VoiceDeafenStateChanged", serverId, channelId, userId, isDeafened);
        await Clients.OthersInGroup(ServerGroup(serverId))
            .SendAsync("VoiceDeafenStateChanged", serverId, channelId, userId, isDeafened);
    }

    public async Task SetSpeakingState(Guid serverId, Guid channelId, bool isSpeaking)
    {
        var (userId, _, _) = GetCallerIdentity();
        await Clients.OthersInGroup(VoiceGroup(serverId, channelId))
            .SendAsync("VoiceSpeakingStateChanged", serverId, channelId, userId, isSpeaking);
        await Clients.OthersInGroup(ServerGroup(serverId))
            .SendAsync("VoiceSpeakingStateChanged", serverId, channelId, userId, isSpeaking);
    }

    // --- Screen Share ---

    public async Task StartScreenShare(Guid serverId, Guid channelId)
    {
        var (userId, username, callerInstanceUrl) = GetCallerIdentity();
        if (!voiceState.SetScreenSharing(serverId, channelId, userId, true))
            return;

        await Clients.OthersInGroup(VoiceGroup(serverId, channelId))
            .SendAsync("ScreenShareStarted", serverId, channelId, userId, username, callerInstanceUrl);
        await Clients.Group(ServerGroup(serverId))
            .SendAsync("ScreenShareStarted", serverId, channelId, userId, username, callerInstanceUrl);

        // Notify remote instances
        var remoteUrls = voiceState.GetRemoteInstanceUrls(serverId, channelId, LocalInstanceUrl);
        foreach (var url in remoteUrls)
        {
            try
            {
                await federationClient.For(url).Voice.NotifyScreenShareAsync(new Fennec.Shared.Dtos.Voice.FederationScreenShareEventDto
                {
                    ServerId = serverId,
                    ChannelId = channelId,
                    UserId = userId,
                    Username = username,
                    InstanceUrl = callerInstanceUrl,
                    IsSharing = true,
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify remote instance {InstanceUrl} of screen share start", url);
            }
        }
    }

    public async Task StopScreenShare(Guid serverId, Guid channelId)
    {
        var (userId, _, _) = GetCallerIdentity();
        voiceState.SetScreenSharing(serverId, channelId, userId, false);

        await Clients.OthersInGroup(VoiceGroup(serverId, channelId))
            .SendAsync("ScreenShareStopped", serverId, channelId, userId);
        await Clients.Group(ServerGroup(serverId))
            .SendAsync("ScreenShareStopped", serverId, channelId, userId);

        // Notify remote instances
        var remoteUrls = voiceState.GetRemoteInstanceUrls(serverId, channelId, LocalInstanceUrl);
        foreach (var url in remoteUrls)
        {
            try
            {
                await federationClient.For(url).Voice.NotifyScreenShareAsync(new Fennec.Shared.Dtos.Voice.FederationScreenShareEventDto
                {
                    ServerId = serverId,
                    ChannelId = channelId,
                    UserId = userId,
                    Username = "",
                    IsSharing = false,
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify remote instance {InstanceUrl} of screen share stop", url);
            }
        }
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("SignalR: Client connected {ConnectionId}", Context.ConnectionId);

        try
        {
            var (userId, username, instanceUrl) = GetCallerIdentity();

            // Skip remote federated connections — only track local users
            if (instanceUrl == null || !IsRemote(instanceUrl))
            {
                var isFirst = globalPresenceService.AddConnection(userId, username, Context.ConnectionId);
                if (isFirst)
                    _ = PushFederatedPresenceAsync(userId, username, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to track global presence for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("SignalR: Client disconnected {ConnectionId} (exception: {Exception})",
            Context.ConnectionId, exception?.Message ?? "none");

        // Global presence cleanup + federated push
        var removed = globalPresenceService.RemoveConnection(Context.ConnectionId);
        if (removed is { IsLast: true })
            _ = PushFederatedPresenceAsync(removed.Value.UserId, removed.Value.Username, false);

        // Per-server presence cleanup
        var offlineUsers = presenceService.RemoveAllByConnectionId(Context.ConnectionId);
        foreach (var (serverId, userId, username, instanceUrl) in offlineUsers)
        {
            await Clients.Group(ServerGroup(serverId)).SendAsync("UserOffline", serverId, userId);
        }

        var voiceRemoved = voiceState.RemoveByConnectionId(Context.ConnectionId);
        if (voiceRemoved is not null)
        {
            var (serverId, channelId, userId) = voiceRemoved.Value;

            // If participant was screen sharing, broadcast stop
            // (RemoveByConnectionId already cleared the sharing state)
            await Clients.Group(VoiceGroup(serverId, channelId))
                .SendAsync("ScreenShareStopped", serverId, channelId, userId);
            await Clients.Group(ServerGroup(serverId))
                .SendAsync("ScreenShareStopped", serverId, channelId, userId);

            // Broadcast locally
            await Clients.Group(VoiceGroup(serverId, channelId))
                .SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
            await Clients.Group(ServerGroup(serverId))
                .SendAsync("VoiceParticipantLeft", serverId, channelId, userId);

            // Check if this participant was in a remote server's voice channel
            // We need to find out which instance hosts this server to forward the leave.
            // We look up any remaining remote instance URLs for this channel to determine
            // if the participant was remote-forwarded. But since we tracked locally, we
            // just need to check if there's a KnownServer entry.
            // For simplicity: try to forward to any remote instances we know about.
            // The GetCallerIdentity would give us the instance URL but we can't call it
            // during disconnect. Instead, we check if there are remote participants and
            // notify them, and also check if this server exists as a KnownServer.

            // Notify remote instances about this participant leaving (if this is the hosting instance)
            await NotifyRemoteInstancesParticipantLeft(serverId, channelId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task PushFederatedPresenceAsync(Guid userId, string username, bool isOnline)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FennecDbContext>();

            var knownUser = await db.Set<KnownUser>()
                .FirstOrDefaultAsync(k => k.RemoteId == userId && k.InstanceUrl == LocalInstanceUrl);

            if (knownUser is null) return;

            var instanceUrls = await db.Set<UserJoinedKnownServer>()
                .Where(u => u.KnownUserId == knownUser.Id)
                .Include(u => u.KnownServer)
                .Select(u => u.KnownServer.InstanceUrl)
                .Distinct()
                .ToListAsync();

            foreach (var url in instanceUrls)
            {
                try
                {
                    await federationClient.For(url).Presence.PushPresenceAsync(userId, username, isOnline);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to push presence to {InstanceUrl}", url);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push federated presence for user {UserId}", userId);
        }
    }

    private async Task NotifyRemoteInstancesParticipantJoined(Guid serverId, Guid channelId, VoiceParticipantDto participant)
    {
        var remoteUrls = voiceState.GetRemoteInstanceUrls(serverId, channelId, LocalInstanceUrl);
        foreach (var url in remoteUrls)
        {
            // Don't notify the participant's own instance
            if (url.Equals(participant.InstanceUrl, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                await federationClient.For(url).Voice.NotifyParticipantJoinedAsync(new FederationVoiceParticipantEventDto
                {
                    ServerId = serverId,
                    ChannelId = channelId,
                    Participant = participant,
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify remote instance {InstanceUrl} of voice join", url);
            }
        }
    }

    private async Task NotifyRemoteInstancesParticipantLeft(Guid serverId, Guid channelId, Guid userId)
    {
        var remoteUrls = voiceState.GetRemoteInstanceUrls(serverId, channelId, LocalInstanceUrl);
        foreach (var url in remoteUrls)
        {
            try
            {
                await federationClient.For(url).Voice.NotifyParticipantLeftAsync(new FederationVoiceParticipantLeftEventDto
                {
                    ServerId = serverId,
                    ChannelId = channelId,
                    UserId = userId,
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify remote instance {InstanceUrl} of voice leave", url);
            }
        }
    }

    private bool IsRemote(string instanceUrl)
    {
        return !instanceUrl.Equals(LocalInstanceUrl, StringComparison.OrdinalIgnoreCase);
    }

    private (Guid UserId, string Username, string? InstanceUrl) GetCallerIdentity()
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext?.Request.Query["access_token"].FirstOrDefault();

        // Fallback: check Authorization header (used by non-WebSocket transports)
        if (token is null)
        {
            var auth = httpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (auth is not null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                token = auth["Bearer ".Length..];
        }

        if (token is null)
            throw new HubException("No access token provided");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var sub = jwt.Subject ?? throw new HubException("Token missing subject");
        var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "Unknown";
        var issuer = jwt.Issuer;
        return (Guid.Parse(sub), name, issuer);
    }

    private static string VoiceGroup(Guid serverId, Guid channelId) => $"voice-{serverId}-{channelId}";
    private static string ServerGroup(Guid serverId) => $"server-{serverId}";
}

public interface IMessageEventService
{
    Task NotifyMessageReceived(Guid serverId, Guid channelId, MessageReceivedDto message,
        CancellationToken cancellationToken);
}

public class MessageEventService(
    IHubContext<MessageHub> messageHubContext,
    ILogger<MessageEventService> logger
) : IMessageEventService
{
    public Task NotifyMessageReceived(Guid serverId, Guid channelId, MessageReceivedDto message, CancellationToken cancellationToken)
    {
        var groupName = $"{serverId}-{channelId}";
        logger.LogInformation("SignalR: Broadcasting MessageReceived to group {Group} (messageId={MessageId}, author={Author})",
            groupName, message.MessageId, message.AuthorName);
        return messageHubContext.Clients.Group(groupName)
            .SendAsync("MessageReceived", serverId, channelId, message, cancellationToken: cancellationToken);
    }
}
