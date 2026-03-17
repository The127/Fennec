using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Fennec.App.Tests.ViewModels;

public class VoiceCallViewModelTests
{
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly IVoiceCallService _voiceCallService = Substitute.For<IVoiceCallService>();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ObservableCollection<ChannelGroupItem> _channelGroups = [];

    private VoiceParticipantsViewModel CreateParticipantsVm() =>
        new(_serverId, _messenger, _channelGroups);

    private VoiceCallViewModel CreateViewModel(VoiceParticipantsViewModel? participants = null) =>
        new(_serverId, "https://fennec.chat", _userId, "alice",
            _voiceCallService, _messenger,
            NullLogger<VoiceCallViewModel>.Instance,
            participants ?? CreateParticipantsVm());

    private ChannelItem AddChannel(Guid channelId, string name = "lounge")
    {
        var channel = new ChannelItem(channelId, name, Fennec.Shared.Models.ChannelType.TextAndVoice, Guid.NewGuid());
        _channelGroups.Add(new ChannelGroupItem(Guid.NewGuid(), "group", [channel]));
        return channel;
    }

    [Fact]
    public void call_state_is_restored_when_already_in_a_call()
    {
        var channelId = Guid.NewGuid();
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.CurrentServerId.Returns(_serverId);
        _voiceCallService.CurrentChannelId.Returns(channelId);
        _voiceCallService.IsMuted.Returns(true);
        _voiceCallService.IsDeafened.Returns(false);

        var vm = CreateViewModel();

        Assert.True(vm.IsInVoiceChannel);
        Assert.Equal(channelId, vm.CurrentVoiceChannelId);
        Assert.True(vm.IsMuted);
        Assert.False(vm.IsDeafened);
    }

    [Fact]
    public void call_state_from_a_different_server_is_not_restored()
    {
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.CurrentServerId.Returns(Guid.NewGuid()); // different server

        var vm = CreateViewModel();

        Assert.False(vm.IsInVoiceChannel);
    }

    [Fact]
    public void muting_toggles_state_and_notifies_the_service()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsMuted);

        vm.ToggleMuteCommand.Execute(null);

        Assert.True(vm.IsMuted);
        _voiceCallService.Received(1).SetMuted(true);
    }

    [Fact]
    public void unmuting_after_muting_restores_audio()
    {
        var vm = CreateViewModel();

        vm.ToggleMuteCommand.Execute(null);
        vm.ToggleMuteCommand.Execute(null);

        Assert.False(vm.IsMuted);
        _voiceCallService.Received(1).SetMuted(false);
    }

    [Fact]
    public void deafening_toggles_state_and_notifies_the_service()
    {
        var vm = CreateViewModel();

        vm.ToggleDeafenCommand.Execute(null);

        Assert.True(vm.IsDeafened);
        _voiceCallService.Received(1).SetDeafened(true);
    }

    [Fact]
    public void deafening_also_mutes_the_microphone()
    {
        var vm = CreateViewModel();

        vm.ToggleDeafenCommand.Execute(null);

        Assert.True(vm.IsMuted);
        Assert.True(vm.IsDeafened);
    }

    [Fact]
    public void mute_state_is_reflected_in_the_channel_participant_list()
    {
        var channelId = Guid.NewGuid();
        var channel = AddChannel(channelId);
        channel.VoiceParticipants.Add(new VoiceParticipantItem(_userId, "alice", null));
        var participants = CreateParticipantsVm();
        var vm = CreateViewModel(participants);
        vm.IsInVoiceChannel = true;
        vm.CurrentVoiceChannelId = channelId;

        vm.ToggleMuteCommand.Execute(null);

        Assert.True(channel.VoiceParticipants[0].IsMuted);
    }

    [Fact]
    public async Task joining_a_channel_connects_to_voice()
    {
        var channelId = Guid.NewGuid();
        var channel = new ChannelItem(channelId, "lounge", Fennec.Shared.Models.ChannelType.TextAndVoice, Guid.NewGuid());
        var vm = CreateViewModel();

        await vm.JoinVoiceChannelCommand.ExecuteAsync(channel);

        await _voiceCallService.Received(1).JoinAsync(_serverId, channelId, "https://fennec.chat", _userId, "alice");
    }

    [Fact]
    public async Task text_only_channels_do_not_support_voice()
    {
        var channel = new ChannelItem(Guid.NewGuid(), "general", Fennec.Shared.Models.ChannelType.TextOnly, Guid.NewGuid());
        var vm = CreateViewModel();

        await vm.JoinVoiceChannelCommand.ExecuteAsync(channel);

        await _voiceCallService.DidNotReceive().JoinAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task joining_a_channel_already_active_has_no_effect()
    {
        var channelId = Guid.NewGuid();
        var channel = new ChannelItem(channelId, "lounge", Fennec.Shared.Models.ChannelType.TextAndVoice, Guid.NewGuid());
        var vm = CreateViewModel();
        vm.IsInVoiceChannel = true;
        vm.CurrentVoiceChannelId = channelId;

        await vm.JoinVoiceChannelCommand.ExecuteAsync(channel);

        await _voiceCallService.DidNotReceive().JoinAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task leaving_a_channel_disconnects_from_voice()
    {
        var vm = CreateViewModel();

        await vm.LeaveVoiceChannelCommand.ExecuteAsync(null);

        await _voiceCallService.Received(1).LeaveAsync();
    }

    [Fact]
    public void channel_name_is_displayed_while_in_a_call()
    {
        var channelId = Guid.NewGuid();
        AddChannel(channelId, "lounge");
        var participants = CreateParticipantsVm();
        var vm = CreateViewModel(participants);
        vm.IsInVoiceChannel = true;
        vm.CurrentVoiceChannelId = channelId;

        vm.RestoreVoiceChannelName();

        Assert.Equal("lounge", vm.CurrentVoiceChannelName);
    }

    [Fact]
    public void no_channel_name_is_shown_when_not_in_a_call()
    {
        var vm = CreateViewModel();

        vm.RestoreVoiceChannelName();

        Assert.Null(vm.CurrentVoiceChannelName);
    }
}
