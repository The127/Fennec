using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.Client;
using Fennec.Shared.Dtos.Voice;

namespace Fennec.App.Services;

public interface IVoiceHubService
{
    Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl);
    Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl);
    Task SendSdpOfferAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendSdpAnswerAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendIceCandidateAsync(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex);

    event Action<Guid, Guid, Guid, string>? SdpOfferReceived;
    event Action<Guid, Guid, Guid, string>? SdpAnswerReceived;
    event Action<Guid, Guid, Guid, string, string?, int?>? IceCandidateReceived;

    void Initialize();
}

public class VoiceHubService : IVoiceHubService
{
    private readonly IMessageHubClient _hubClient;
    private readonly IMessenger _messenger;

    public event Action<Guid, Guid, Guid, string>? SdpOfferReceived;
    public event Action<Guid, Guid, Guid, string>? SdpAnswerReceived;
    public event Action<Guid, Guid, Guid, string, string?, int?>? IceCandidateReceived;

    public VoiceHubService(IMessageHubClient hubClient, IMessenger messenger)
    {
        _hubClient = hubClient;
        _messenger = messenger;
    }

    public void Initialize()
    {
        _hubClient.VoiceParticipantJoined += (serverId, channelId, participant) =>
        {
            _messenger.Send(new VoiceParticipantJoinedMessage(serverId, channelId, participant.UserId, participant.Username, participant.InstanceUrl));
        };

        _hubClient.VoiceParticipantLeft += (serverId, channelId, userId) =>
        {
            _messenger.Send(new VoiceParticipantLeftMessage(serverId, channelId, userId));
        };

        _hubClient.SdpOfferReceived += (serverId, channelId, fromUserId, sdp) =>
        {
            SdpOfferReceived?.Invoke(serverId, channelId, fromUserId, sdp);
        };

        _hubClient.SdpAnswerReceived += (serverId, channelId, fromUserId, sdp) =>
        {
            SdpAnswerReceived?.Invoke(serverId, channelId, fromUserId, sdp);
        };

        _hubClient.IceCandidateReceived += (serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex) =>
        {
            IceCandidateReceived?.Invoke(serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex);
        };
    }

    public Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl)
        => _hubClient.JoinVoiceChannelAsync(serverId, channelId, instanceUrl);

    public Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl)
        => _hubClient.LeaveVoiceChannelAsync(serverId, channelId, instanceUrl);

    public Task SendSdpOfferAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
        => _hubClient.SendSdpOfferAsync(serverId, channelId, targetUserId, sdp);

    public Task SendSdpAnswerAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
        => _hubClient.SendSdpAnswerAsync(serverId, channelId, targetUserId, sdp);

    public Task SendIceCandidateAsync(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
        => _hubClient.SendIceCandidateAsync(serverId, channelId, targetUserId, candidate, sdpMid, sdpMLineIndex);
}
