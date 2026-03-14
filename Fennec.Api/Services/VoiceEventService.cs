using Fennec.Api.Hubs;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.SignalR;

namespace Fennec.Api.Services;

public interface IVoiceEventService
{
    Task NotifyParticipantJoined(Guid serverId, Guid channelId, VoiceParticipantDto participant);
    Task NotifyParticipantLeft(Guid serverId, Guid channelId, Guid userId);
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
}
