using System.IdentityModel.Tokens.Jwt;
using Fennec.Api.Services;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.SignalR;

namespace Fennec.Api.Hubs;

public class MessageHub(VoiceStateService voiceState, ILogger<MessageHub> logger) : Hub
{
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
    }

    public async Task UnsubscribeFromServer(Guid serverId)
    {
        var groupName = ServerGroup(serverId);
        logger.LogInformation("SignalR: Connection {ConnectionId} unsubscribing from server group {Group}", Context.ConnectionId, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public Dictionary<Guid, List<VoiceParticipantDto>> GetServerVoiceState(Guid serverId)
    {
        return voiceState.GetServerVoiceState(serverId);
    }

    // --- Voice ---

    public async Task<List<VoiceParticipantDto>> JoinVoiceChannel(Guid serverId, Guid channelId)
    {
        try
        {
            var (userId, username) = GetCallerIdentity();
            var groupName = VoiceGroup(serverId, channelId);

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            var participants = voiceState.AddParticipant(serverId, channelId, userId, username, Context.ConnectionId);

            var participantDto = new VoiceParticipantDto { UserId = userId, Username = username };
            await Clients.OthersInGroup(groupName).SendAsync("VoiceParticipantJoined", serverId, channelId, participantDto);
            await Clients.Group(ServerGroup(serverId)).SendAsync("VoiceParticipantJoined", serverId, channelId, participantDto);

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

    public async Task LeaveVoiceChannel(Guid serverId, Guid channelId)
    {
        var (userId, _) = GetCallerIdentity();
        var groupName = VoiceGroup(serverId, channelId);

        voiceState.RemoveParticipant(serverId, channelId, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        await Clients.OthersInGroup(groupName).SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
        await Clients.Group(ServerGroup(serverId)).SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
    }

    public async Task SendSdpOffer(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        var (userId, _) = GetCallerIdentity();
        var groupName = VoiceGroup(serverId, channelId);

        // Find target connection
        var participants = voiceState.GetParticipants(serverId, channelId);
        // We relay to the group but filter by targetUserId on the client side —
        // however, for efficiency, we should send directly. Since we don't have a userId→connectionId map
        // exposed, we'll use the group and let clients filter.
        await Clients.OthersInGroup(groupName).SendAsync("ReceiveSdpOffer", serverId, channelId, userId, sdp);
    }

    public async Task SendSdpAnswer(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        var (userId, _) = GetCallerIdentity();
        await Clients.OthersInGroup(VoiceGroup(serverId, channelId))
            .SendAsync("ReceiveSdpAnswer", serverId, channelId, userId, sdp);
    }

    public async Task SendIceCandidate(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        var (userId, _) = GetCallerIdentity();
        await Clients.OthersInGroup(VoiceGroup(serverId, channelId))
            .SendAsync("ReceiveIceCandidate", serverId, channelId, userId, candidate, sdpMid, sdpMLineIndex);
    }

    public override Task OnConnectedAsync()
    {
        logger.LogInformation("SignalR: Client connected {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("SignalR: Client disconnected {ConnectionId} (exception: {Exception})",
            Context.ConnectionId, exception?.Message ?? "none");

        var removed = voiceState.RemoveByConnectionId(Context.ConnectionId);
        if (removed is not null)
        {
            var (serverId, channelId, userId) = removed.Value;
            await Clients.Group(VoiceGroup(serverId, channelId))
                .SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
            await Clients.Group(ServerGroup(serverId))
                .SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private (Guid UserId, string Username) GetCallerIdentity()
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
        return (Guid.Parse(sub), name);
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
