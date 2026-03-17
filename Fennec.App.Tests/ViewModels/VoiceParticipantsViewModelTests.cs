using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.ViewModels;

namespace Fennec.App.Tests.ViewModels;

public class VoiceParticipantsViewModelTests
{
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly ObservableCollection<ChannelGroupItem> _channelGroups = [];

    private VoiceParticipantsViewModel CreateViewModel() =>
        new(_serverId, _messenger, _channelGroups);

    private ChannelItem AddChannel(Guid channelId, string name = "lounge")
    {
        var channel = new ChannelItem(channelId, name, Fennec.Shared.Models.ChannelType.TextAndVoice, Guid.NewGuid());
        _channelGroups.Add(new ChannelGroupItem(Guid.NewGuid(), "group", [channel]));
        return channel;
    }

    [Fact]
    public void channel_is_unresolvable_before_groups_are_loaded()
    {
        var vm = CreateViewModel();

        var result = vm.FindChannel(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void channel_resolves_by_id_when_loaded()
    {
        var channelId = Guid.NewGuid();
        AddChannel(channelId);
        var vm = CreateViewModel();

        var result = vm.FindChannel(channelId);

        Assert.NotNull(result);
        Assert.Equal(channelId, result!.Id);
    }

    [Fact]
    public void unknown_channel_id_does_not_resolve()
    {
        AddChannel(Guid.NewGuid());
        var vm = CreateViewModel();

        var result = vm.FindChannel(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void local_mute_state_is_reflected_in_participant_display()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channel = AddChannel(channelId);
        channel.VoiceParticipants.Add(new VoiceParticipantItem(userId, "alice", null));
        var vm = CreateViewModel();

        vm.UpdateLocalParticipantMuteState(isMuted: true, isDeafened: false, userId, channelId);

        Assert.True(channel.VoiceParticipants[0].IsMuted);
        Assert.False(channel.VoiceParticipants[0].IsDeafened);
    }

    [Fact]
    public void mute_state_update_when_not_in_a_channel_is_a_no_op()
    {
        var vm = CreateViewModel();

        // Should not throw
        vm.UpdateLocalParticipantMuteState(true, true, Guid.NewGuid(), null);
    }

    [Fact]
    public void mute_state_update_for_unrecognized_channel_is_a_no_op()
    {
        var channelId = Guid.NewGuid();
        var vm = CreateViewModel();

        // Should not throw
        vm.UpdateLocalParticipantMuteState(true, true, Guid.NewGuid(), channelId);
    }

    [Fact]
    public void speaking_indicators_are_cleared_on_reset()
    {
        var channel = AddChannel(Guid.NewGuid());
        channel.VoiceParticipants.Add(new VoiceParticipantItem(Guid.NewGuid(), "alice", null) { IsSpeaking = true });
        channel.VoiceParticipants.Add(new VoiceParticipantItem(Guid.NewGuid(), "bob", null) { IsSpeaking = true });
        var vm = CreateViewModel();

        vm.ResetSpeakingIndicators();

        Assert.All(channel.VoiceParticipants, p => Assert.False(p.IsSpeaking));
    }

    [Fact]
    public void joining_participants_from_other_servers_are_not_shown()
    {
        var channelId = Guid.NewGuid();
        AddChannel(channelId);
        var vm = CreateViewModel();

        _messenger.Send(new VoiceParticipantJoinedMessage(Guid.NewGuid(), channelId, Guid.NewGuid(), "alice", null));

        Assert.Empty(_channelGroups[0].Channels[0].VoiceParticipants);
    }

    [Fact]
    public void mute_events_from_other_servers_do_not_affect_local_participants()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channel = AddChannel(channelId);
        channel.VoiceParticipants.Add(new VoiceParticipantItem(userId, "alice", null) { IsMuted = false });
        var vm = CreateViewModel();

        _messenger.Send(new VoiceMuteStateChangedMessage(Guid.NewGuid(), channelId, userId, true));

        Assert.False(channel.VoiceParticipants[0].IsMuted);
    }
}
