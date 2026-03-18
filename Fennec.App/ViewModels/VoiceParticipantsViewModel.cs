using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain.Events;
using Fennec.App.Messages;

namespace Fennec.App.ViewModels;

public class VoiceParticipantsViewModel :
    IRecipient<VoiceParticipantJoinedMessage>,
    IRecipient<VoiceParticipantLeftMessage>,
    IRecipient<VoiceMuteStateChangedMessage>,
    IRecipient<VoiceDeafenStateChangedMessage>,
    IRecipient<VoiceSpeakingChangedMessage>,
    IRecipient<VoicePeerStateChangedMessage>
{
    private readonly Guid _serverId;
    private readonly ObservableCollection<ChannelGroupItem> _channelGroups;

    public VoiceParticipantsViewModel(Guid serverId, IMessenger messenger, ObservableCollection<ChannelGroupItem> channelGroups)
    {
        _serverId = serverId;
        _channelGroups = channelGroups;

        messenger.Register<VoiceParticipantJoinedMessage>(this);
        messenger.Register<VoiceParticipantLeftMessage>(this);
        messenger.Register<VoiceMuteStateChangedMessage>(this);
        messenger.Register<VoiceDeafenStateChangedMessage>(this);
        messenger.Register<VoiceSpeakingChangedMessage>(this);
        messenger.Register<VoicePeerStateChangedMessage>(this);
    }

    public ChannelItem? FindChannel(Guid channelId)
    {
        foreach (var group in _channelGroups)
        {
            var channel = group.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel is not null) return channel;
        }
        return null;
    }

    public void UpdateLocalParticipantMuteState(bool isMuted, bool isDeafened, Guid currentUserId, Guid? currentVoiceChannelId)
    {
        if (currentVoiceChannelId is null) return;
        var channel = FindChannel(currentVoiceChannelId.Value);
        var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == currentUserId);
        if (participant is not null)
        {
            participant.IsMuted = isMuted;
            participant.IsDeafened = isDeafened;
        }
    }

    public void ResetSpeakingIndicators()
    {
        foreach (var group in _channelGroups)
            foreach (var ch in group.Channels)
                foreach (var p in ch.VoiceParticipants)
                    p.IsSpeaking = false;
    }

    public void Receive(VoiceParticipantJoinedMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            if (channel is null) return;

            if (channel.VoiceParticipants.Any(p => p.UserId == message.UserId))
                return;

            channel.VoiceParticipants.Add(new VoiceParticipantItem(message.UserId, message.Username, message.InstanceUrl)
            {
                IsMuted = message.IsMuted,
                IsDeafened = message.IsDeafened,
                IsScreenSharing = message.IsScreenSharing,
            });
        });
    }

    public void Receive(VoiceParticipantLeftMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            if (channel is null) return;

            var participant = channel.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                channel.VoiceParticipants.Remove(participant);
        });
    }

    public void Receive(VoiceMuteStateChangedMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsMuted = message.IsMuted;
        });
    }

    public void Receive(VoiceDeafenStateChangedMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsDeafened = message.IsDeafened;
        });
    }

    public void Receive(VoiceSpeakingChangedMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.IsSpeaking = message.IsSpeaking;
        });
    }

    public void Receive(VoicePeerStateChangedMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var channel = FindChannel(message.ChannelId);
            var participant = channel?.VoiceParticipants.FirstOrDefault(p => p.UserId == message.UserId);
            if (participant is not null)
                participant.PeerState = message.State;
        });
    }
}
