using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class ScreenShareBroadcastViewModelTests
{
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly IVoiceCallService _voiceCallService = Substitute.For<IVoiceCallService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private ScreenShareBroadcastViewModel CreateViewModel() =>
        new(_serverId, _userId, _voiceCallService, _settingsStore, new DialogManager(), _messenger,
            NullLogger<ScreenShareBroadcastViewModel>.Instance);

    [Fact]
    public void shows_as_sharing_when_returning_to_a_server_already_broadcasting()
    {
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.CurrentServerId.Returns(_serverId);
        _voiceCallService.IsScreenSharing.Returns(true);

        var vm = CreateViewModel();

        Assert.True(vm.IsScreenSharing);
    }

    [Fact]
    public void broadcast_state_is_not_restored_for_a_different_server()
    {
        _voiceCallService.IsConnected.Returns(true);
        _voiceCallService.CurrentServerId.Returns(Guid.NewGuid());
        _voiceCallService.IsScreenSharing.Returns(true);

        var vm = CreateViewModel();

        Assert.False(vm.IsScreenSharing);
    }

    [AvaloniaFact]
    public void indicator_shows_sharing_when_own_broadcast_starts()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsScreenSharing);

        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsScreenSharing);
    }

    [AvaloniaFact]
    public void others_broadcasts_do_not_affect_own_sharing_indicator()
    {
        var vm = CreateViewModel();

        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), Guid.NewGuid(), "bob", null));
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsScreenSharing);
    }

    [AvaloniaFact]
    public void broadcasts_on_other_servers_do_not_affect_sharing_indicator()
    {
        var vm = CreateViewModel();

        _messenger.Send(new ScreenShareStartedMessage(Guid.NewGuid(), Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsScreenSharing);
    }

    [AvaloniaFact]
    public void indicator_clears_when_own_broadcast_stops()
    {
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsScreenSharing);

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), _userId));
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsScreenSharing);
    }

    [AvaloniaFact]
    public void others_stopping_does_not_affect_own_sharing_indicator()
    {
        var vm = CreateViewModel();
        _messenger.Send(new ScreenShareStartedMessage(_serverId, Guid.NewGuid(), _userId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        _messenger.Send(new ScreenShareStoppedMessage(_serverId, Guid.NewGuid(), Guid.NewGuid()));
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsScreenSharing);
    }

    [Fact]
    public async Task stopping_broadcast_notifies_the_voice_service()
    {
        var vm = CreateViewModel();

        await vm.StopScreenShareCommand.ExecuteAsync(null);

        await _voiceCallService.Received(1).StopScreenShareAsync();
    }
}
