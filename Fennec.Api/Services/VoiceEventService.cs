using Fennec.Api.Hubs;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.SignalR;

namespace Fennec.Api.Services;

public interface IVoiceEventService
{
    Task NotifyParticipantJoined(Guid serverId, Guid channelId, VoiceParticipantDto participant);
    Task NotifyParticipantLeft(Guid serverId, Guid channelId, Guid userId);
    Task NotifyScreenShareStarted(Guid serverId, Guid channelId, Guid userId, string username, string? instanceUrl);
    Task NotifyScreenShareStopped(Guid serverId, Guid channelId, Guid userId);
}

public class VoiceEventService(IHubContext<MessageHub> hubContext) : IVoiceEventService
{
    public async Task NotifyParticipantJoined(Guid serverId, Guid channelId, VoiceParticipantDto participant)
    {
        var serverGroup = $"server-{serverId}";
        var voiceGroup = $"voice-{serverId}-{channelId}";
        await hubContext.Clients.Group(voiceGroup).SendAsync("VoiceParticipantJoined", serverId, channelId, participant);
        await hubContext.Clients.Group(serverGroup).SendAsync("VoiceParticipantJoined", serverId, channelId, participant);
    }

    public async Task NotifyParticipantLeft(Guid serverId, Guid channelId, Guid userId)
    {
        var serverGroup = $"server-{serverId}";
        var voiceGroup = $"voice-{serverId}-{channelId}";
        await hubContext.Clients.Group(voiceGroup).SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
        await hubContext.Clients.Group(serverGroup).SendAsync("VoiceParticipantLeft", serverId, channelId, userId);
    }

    public async Task NotifyScreenShareStarted(Guid serverId, Guid channelId, Guid userId, string username, string? instanceUrl)
    {
        var serverGroup = $"server-{serverId}";
        var voiceGroup = $"voice-{serverId}-{channelId}";
        await hubContext.Clients.Group(voiceGroup).SendAsync("ScreenShareStarted", serverId, channelId, userId, username, instanceUrl);
        await hubContext.Clients.Group(serverGroup).SendAsync("ScreenShareStarted", serverId, channelId, userId, username, instanceUrl);
    }

    public async Task NotifyScreenShareStopped(Guid serverId, Guid channelId, Guid userId)
    {
        var serverGroup = $"server-{serverId}";
        var voiceGroup = $"voice-{serverId}-{channelId}";
        await hubContext.Clients.Group(voiceGroup).SendAsync("ScreenShareStopped", serverId, channelId, userId);
        await hubContext.Clients.Group(serverGroup).SendAsync("ScreenShareStopped", serverId, channelId, userId);
    }
}
