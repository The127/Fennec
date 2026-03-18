using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain.Events;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Fennec.App.Tests.ViewModels;

public class ScreenShareWatcherViewModelTests
{
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly IVoiceCallService _voiceCallService = Substitute.For<IVoiceCallService>();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ObservableCollection<ChannelGroupItem> _channelGroups = [];

    private ChannelItem? _findChannelResult;
    private ChannelItem? FindChannel(Guid _) => _findChannelResult;

    private ScreenShareWatcherViewModel CreateViewModel() =>
        new(_serverId, _userId, _voiceCallService, _messenger,
            NullLogger<ScreenShareWatcherViewModel>.Instance, FindChannel);

    [Fact]
    public void active_broadcasts_are_visible_when_joining_mid_session()
    {
        var sharerId = Guid.NewGuid();
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.CurrentServerId.Returns(_serverId);
        _voiceCallService.ActiveScreenSharers.Returns(new List<ActiveScreenSharer>
        {
            new(sharerId, "alice", null),
        });

        var vm = CreateViewModel();

        Assert.Single(vm.ActiveScreenShares);
        Assert.Equal(sharerId, vm.ActiveScreenShares[0].UserId);
    }

    [AvaloniaFact]
    public void another_users_broadcast_appears_in_the_available_list()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();

        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.ActiveScreenShares);
        Assert.Equal(sharerId, vm.ActiveScreenShares[0].UserId);
    }

    [AvaloniaFact]
    public void own_broadcast_does_not_appear_in_the_watchable_list()
    {
        var vm = CreateViewModel();

        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "me", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.ActiveScreenShares);
    }

    [AvaloniaFact]
    public void broadcasts_from_other_servers_are_not_shown()
    {
        var vm = CreateViewModel();

        _messenger.Send(new ScreenShareStartedMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.ActiveScreenShares);
    }

    [AvaloniaFact]
    public void duplicate_broadcast_start_events_appear_only_once()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();

        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.ActiveScreenShares);
    }

    [AvaloniaFact]
    public void ended_broadcast_is_removed_from_the_available_list()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), sharerId));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.ActiveScreenShares);
    }

    [AvaloniaFact]
    public void stopping_own_broadcast_does_not_affect_others_in_the_list()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), _userId));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.ActiveScreenShares); // other's share unaffected
    }

    [AvaloniaFact]
    public void watching_a_broadcast_adds_it_to_active_watches_and_focuses_it()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        vm.WatchScreenShareCommand.Execute(sharerId);

        Assert.Single(vm.WatchedScreenShares);
        Assert.True(vm.HasWatchedShares);
        Assert.Equal(sharerId, vm.FocusedScreenShareUserId);
    }

    [AvaloniaFact]
    public void watching_the_same_broadcast_twice_only_watches_it_once()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        vm.WatchScreenShareCommand.Execute(sharerId);
        vm.WatchScreenShareCommand.Execute(sharerId);

        Assert.Single(vm.WatchedScreenShares);
    }

    [AvaloniaFact]
    public void unwatching_a_broadcast_removes_it_from_active_watches()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        vm.WatchScreenShareCommand.Execute(sharerId);

        vm.UnwatchScreenShareCommand.Execute(sharerId);

        Assert.Empty(vm.WatchedScreenShares);
        Assert.False(vm.HasWatchedShares);
    }

    [AvaloniaFact]
    public void fullscreen_collapses_when_the_last_watched_broadcast_is_removed()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        vm.WatchScreenShareCommand.Execute(sharerId);
        vm.IsScreenShareMaximized = true;

        vm.UnwatchScreenShareCommand.Execute(sharerId);

        Assert.False(vm.IsScreenShareMaximized);
    }

    [AvaloniaFact]
    public void broadcast_stopping_mid_watch_removes_it_automatically()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        vm.WatchScreenShareCommand.Execute(sharerId);

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), sharerId));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.WatchedScreenShares);
        Assert.False(vm.HasWatchedShares);
    }

    [Fact]
    public void focusing_a_broadcast_makes_it_the_active_view()
    {
        var userId = Guid.NewGuid();
        var vm = CreateViewModel();

        vm.FocusScreenShareCommand.Execute(userId);

        Assert.Equal(userId, vm.FocusedScreenShareUserId);
    }

    [Fact]
    public void maximizing_toggles_fullscreen_mode()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsScreenShareMaximized);

        vm.ToggleScreenShareMaximizeCommand.Execute(null);

        Assert.True(vm.IsScreenShareMaximized);
    }

    [Fact]
    public void exiting_fullscreen_closes_maximized_view()
    {
        var vm = CreateViewModel();
        vm.IsScreenShareMaximized = true;

        vm.ExitScreenShareMaximizeCommand.Execute(null);

        Assert.False(vm.IsScreenShareMaximized);
    }

    [Fact]
    public void toggling_tile_view_switches_layout_mode()
    {
        var vm = CreateViewModel();

        vm.ToggleTileViewCommand.Execute(null);

        Assert.True(vm.ShowTileView);
    }

    [AvaloniaFact]
    public void popping_out_a_broadcast_removes_it_from_main_focus()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        vm.WatchScreenShareCommand.Execute(sharerId);
        Assert.Equal(sharerId, vm.FocusedScreenShareUserId);

        _messenger.Send(new ScreenSharePopOutRequestedMessage(sharerId, "alice"));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(vm.FocusedScreenShareUserId);
    }

    [AvaloniaFact]
    public void closing_a_popped_out_broadcast_restores_focus_if_still_watched()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        vm.WatchScreenShareCommand.Execute(sharerId);
        _messenger.Send(new ScreenSharePopOutRequestedMessage(sharerId, "alice"));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(vm.FocusedScreenShareUserId);

        _messenger.Send(new ScreenSharePopOutClosedMessage(sharerId));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(sharerId, vm.FocusedScreenShareUserId);
    }

    [AvaloniaFact]
    public void leaving_voice_clears_all_screen_share_state()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        vm.WatchScreenShareCommand.Execute(sharerId);
        vm.IsScreenShareMaximized = true;

        _messenger.Send(new VoiceStateChangedMessage(false, _serverId, null));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.ActiveScreenShares);
        Assert.Empty(vm.WatchedScreenShares);
        Assert.False(vm.HasWatchedShares);
        Assert.False(vm.IsScreenShareMaximized);
        Assert.Null(vm.FocusedScreenShareUserId);
    }

    [AvaloniaFact]
    public void reconnecting_to_voice_preserves_active_broadcasts()
    {
        var sharerId = Guid.NewGuid();
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), sharerId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        _messenger.Send(new VoiceStateChangedMessage(true, _serverId, Guid.NewGuid()));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.ActiveScreenShares);
    }
}
