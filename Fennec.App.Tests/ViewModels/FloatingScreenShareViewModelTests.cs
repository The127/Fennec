using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain.Events;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using NSubstitute;

namespace Fennec.App.Tests.ViewModels;

public class FloatingScreenShareViewModelTests
{
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly IVoiceCallService _voiceCallService = Substitute.For<IVoiceCallService>();
    private readonly IVoiceChannelNavigator _navigator = Substitute.For<IVoiceChannelNavigator>();
    private readonly IRouter _router = Substitute.For<IRouter>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private FloatingScreenShareViewModel CreateViewModel() =>
        new(_messenger, _voiceCallService, _navigator, _router);

    private FloatingScreenShareViewModel CreateViewModelInVoiceCall()
    {
        var vm = CreateViewModel();
        _messenger.Send(new VoiceStateChangedMessage(true, _serverId, Guid.NewGuid()));
        return vm;
    }

    [AvaloniaFact]
    public void pip_appears_when_someone_starts_sharing_on_the_active_voice_server()
    {
        var vm = CreateViewModelInVoiceCall();

        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowFloatingScreenShare);
    }

    [AvaloniaFact]
    public void shares_from_other_servers_are_ignored()
    {
        var vm = CreateViewModelInVoiceCall();

        _messenger.Send(new ScreenShareStartedMessage(Guid.NewGuid(), Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.ShowFloatingScreenShare);
    }

    [AvaloniaFact]
    public void pip_hides_when_the_only_sharer_stops()
    {
        var vm = CreateViewModelInVoiceCall();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), _userId));
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.ShowFloatingScreenShare);
    }

    [AvaloniaFact]
    public void pip_stays_when_one_of_several_sharers_stops()
    {
        var otherId = Guid.NewGuid();
        var vm = CreateViewModelInVoiceCall();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), otherId, "bob", null));
        Dispatcher.UIThread.RunJobs();

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), otherId));
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowFloatingScreenShare);
    }

    [AvaloniaFact]
    public void first_sharer_to_start_is_shown_in_the_pip()
    {
        var vm = CreateViewModelInVoiceCall();

        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("alice", vm.FloatingSharerUsername);
    }

    [AvaloniaFact]
    public void sharer_name_clears_when_the_focused_sharer_stops()
    {
        var vm = CreateViewModelInVoiceCall();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), _userId));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(vm.FloatingSharerUsername);
    }

    [AvaloniaFact]
    public void voice_call_ending_clears_and_hides_the_pip()
    {
        var vm = CreateViewModelInVoiceCall();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowFloatingScreenShare);

        vm.ResetOnCallEnd();

        Assert.False(vm.ShowFloatingScreenShare);
        Assert.Null(vm.FloatingSharerUsername);
    }

    [AvaloniaFact]
    public void pip_visibility_is_re_evaluated_on_route_change()
    {
        var vm = CreateViewModelInVoiceCall();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowFloatingScreenShare);

        vm.OnRouteNavigated();

        Assert.True(vm.ShowFloatingScreenShare);
    }

    [AvaloniaFact]
    public void clicking_go_to_channel_navigates_to_the_voice_server()
    {
        var vm = CreateViewModel();

        vm.NavigateToFloatingScreenShareCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        _navigator.Received(1).NavigateToVoiceChannelAsync();
    }
}
