using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using NSubstitute;

namespace Fennec.App.Tests.ViewModels;

public class VoiceBarViewModelTests
{
    private readonly IVoiceCallService _voiceCallService = Substitute.For<IVoiceCallService>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly IVoiceChannelNavigator _navigator = Substitute.For<IVoiceChannelNavigator>();
    private readonly ObservableCollection<SidebarServer> _servers = [];
    private readonly FloatingScreenShareViewModel _floatingScreenShare;

    private readonly Guid _serverId = Guid.NewGuid();

    public VoiceBarViewModelTests()
    {
        _floatingScreenShare = new FloatingScreenShareViewModel(
            _messenger, Substitute.For<IVoiceCallService>(), _navigator, Substitute.For<IRouter>());
    }

    private VoiceBarViewModel CreateViewModel() =>
        new(_voiceCallService, _messenger, _navigator, _servers, _floatingScreenShare);

    [Fact]
    public void joining_a_voice_call_shows_the_server_name_in_the_bar()
    {
        _servers.Add(new SidebarServer(_serverId, "My Server", "https://fennec.chat"));
        var vm = CreateViewModel();

        _messenger.Send(new VoiceStateChangedMessage(true, _serverId, Guid.NewGuid()));

        Assert.True(vm.IsInVoiceCall);
        Assert.Equal("My Server", vm.VoiceServerName);
    }

    [Fact]
    public void leaving_a_voice_call_hides_the_bar_and_clears_the_server_name()
    {
        _servers.Add(new SidebarServer(_serverId, "My Server", "https://fennec.chat"));
        var vm = CreateViewModel();
        _messenger.Send(new VoiceStateChangedMessage(true, _serverId, Guid.NewGuid()));

        _messenger.Send(new VoiceStateChangedMessage(false, null, null));

        Assert.False(vm.IsInVoiceCall);
        Assert.Null(vm.VoiceServerName);
        Assert.Null(vm.VoiceChannelName);
        Assert.False(vm.IsVoiceMuted);
        Assert.False(vm.IsVoiceDeafened);
    }

    [Fact]
    public void mute_indicator_reflects_the_current_mute_state()
    {
        var vm = CreateViewModel();

        _messenger.Send(new VoiceMuteToggledMessage(true));
        Assert.True(vm.IsVoiceMuted);

        _messenger.Send(new VoiceMuteToggledMessage(false));
        Assert.False(vm.IsVoiceMuted);
    }

    [Fact]
    public void deafening_also_shows_as_muted()
    {
        var vm = CreateViewModel();

        _messenger.Send(new VoiceDeafenToggledMessage(true));

        Assert.True(vm.IsVoiceDeafened);
        Assert.True(vm.IsVoiceMuted);
    }

    [Fact]
    public void undeafening_does_not_automatically_unmute()
    {
        var vm = CreateViewModel();
        _messenger.Send(new VoiceDeafenToggledMessage(true));

        _messenger.Send(new VoiceDeafenToggledMessage(false));

        Assert.False(vm.IsVoiceDeafened);
    }

    [Fact]
    public void toggling_mute_while_in_a_call_broadcasts_and_updates_the_service()
    {
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.IsMuted.Returns(false);
        VoiceMuteToggledMessage? received = null;
        _messenger.Register<VoiceMuteToggledMessage>(this, (_, m) => received = m);
        var vm = CreateViewModel();

        vm.ToggleVoiceMuteCommand.Execute(null);

        _voiceCallService.Received(1).SetMuted(true);
        Assert.True(received?.IsMuted);
    }

    [Fact]
    public void mute_toggle_outside_a_call_is_ignored()
    {
        _voiceCallService.IsConnected.Returns(false);
        var vm = CreateViewModel();

        vm.ToggleVoiceMuteCommand.Execute(null);

        _voiceCallService.DidNotReceive().SetMuted(Arg.Any<bool>());
    }

    [Fact]
    public void toggling_deafen_while_in_a_call_broadcasts_and_updates_the_service()
    {
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.IsDeafened.Returns(false);
        VoiceDeafenToggledMessage? received = null;
        _messenger.Register<VoiceDeafenToggledMessage>(this, (_, m) => received = m);
        var vm = CreateViewModel();

        vm.ToggleVoiceDeafenCommand.Execute(null);

        _voiceCallService.Received(1).SetDeafened(true);
        Assert.True(received?.IsDeafened);
    }

    [Fact]
    public async Task leaving_the_call_disconnects_from_the_voice_service()
    {
        var vm = CreateViewModel();

        await vm.LeaveVoiceCallCommand.ExecuteAsync(null);

        await _voiceCallService.Received(1).LeaveAsync();
    }

    [Fact]
    public async Task clicking_go_to_channel_navigates_to_the_voice_server()
    {
        var vm = CreateViewModel();

        await vm.NavigateToVoiceChannelCommand.ExecuteAsync(null);

        await _navigator.Received(1).NavigateToVoiceChannelAsync();
    }

    [AvaloniaFact]
    public void leaving_a_voice_call_resets_the_floating_screen_share_pip()
    {
        var vm = CreateViewModel();
        _messenger.Send(new VoiceStateChangedMessage(true, _serverId, Guid.NewGuid()));
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), Guid.NewGuid(), "alice", null));
        Dispatcher.UIThread.RunJobs();
        Assert.True(_floatingScreenShare.ShowFloatingScreenShare);

        _messenger.Send(new VoiceStateChangedMessage(false, null, null));

        Assert.False(_floatingScreenShare.ShowFloatingScreenShare);
    }

    [Fact]
    public void bar_shows_current_call_state_when_created_while_a_call_is_active()
    {
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.IsMuted.Returns(true);
        _voiceCallService.IsDeafened.Returns(false);

        var vm = CreateViewModel();

        Assert.True(vm.IsInVoiceCall);
        Assert.True(vm.IsVoiceMuted);
        Assert.False(vm.IsVoiceDeafened);
    }
}
