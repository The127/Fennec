using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.ViewModels;
using Fennec.Shared.Dtos.Server;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class ServerPresenceViewModelTests
{
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly Guid _serverId = Guid.NewGuid();

    private ServerPresenceViewModel CreateViewModel() =>
        new(_serverId, _messenger, new ToastManager());

    private static List<ListServerMembersResponseItemDto> Members(params string[] names) =>
        names.Select(n => new ListServerMembersResponseItemDto { Name = n }).ToList();

    private static List<ServerPresenceEntryDto> Presence(params (Guid Id, string Name)[] entries) =>
        entries.Select(e => new ServerPresenceEntryDto { UserId = e.Id, Username = e.Name }).ToList();

    [Fact]
    public void all_server_members_are_listed()
    {
        var vm = CreateViewModel();

        vm.SetMembers(Members("alice", "bob"));

        Assert.Equal(["alice", "bob"], vm.ServerMembers);
    }

    [Fact]
    public void members_without_an_active_session_appear_offline()
    {
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice", "bob"));

        vm.SetPresence([]);

        Assert.Empty(vm.OnlineMembers);
        Assert.Equal(2, vm.OfflineMembers.Count);
        Assert.Equal(0, vm.OnlineMemberCount);
        Assert.Equal(2, vm.OfflineMemberCount);
    }

    [Fact]
    public void members_with_active_sessions_appear_in_the_online_section()
    {
        var aliceId = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice", "bob", "carol"));

        vm.SetPresence(Presence((aliceId, "alice")));

        Assert.Single(vm.OnlineMembers);
        Assert.Equal("alice", vm.OnlineMembers[0].Username);
        Assert.Equal(2, vm.OfflineMembers.Count);
        Assert.Equal(1, vm.OnlineMemberCount);
        Assert.Equal(2, vm.OfflineMemberCount);
    }

    [Fact]
    public void presence_reflects_current_state_not_accumulated_history()
    {
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice", "bob"));

        vm.SetPresence(Presence((aliceId, "alice")));
        vm.SetPresence(Presence((bobId, "bob")));

        Assert.Single(vm.OnlineMembers);
        Assert.Equal("bob", vm.OnlineMembers[0].Username);
    }

    [AvaloniaFact]
    public void member_becomes_online_when_they_connect()
    {
        var aliceId = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice"));
        vm.SetPresence([]);
        Assert.Empty(vm.OnlineMembers);

        _messenger.Send(new UserOnlineMessage(_serverId, aliceId, "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.OnlineMembers);
        Assert.Equal("alice", vm.OnlineMembers[0].Username);
        Assert.Empty(vm.OfflineMembers);
    }

    [AvaloniaFact]
    public void connections_on_other_servers_do_not_affect_presence()
    {
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice"));
        vm.SetPresence([]);

        _messenger.Send(new UserOnlineMessage(Guid.NewGuid(), Guid.NewGuid(), "alice", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.OnlineMembers);
    }

    [AvaloniaFact]
    public void member_becomes_offline_when_they_disconnect()
    {
        var aliceId = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice"));
        vm.SetPresence(Presence((aliceId, "alice")));
        Assert.Single(vm.OnlineMembers);

        _messenger.Send(new UserOfflineMessage(_serverId, aliceId));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.OnlineMembers);
        Assert.Single(vm.OfflineMembers);
    }

    [AvaloniaFact]
    public void disconnections_on_other_servers_do_not_affect_presence()
    {
        var aliceId = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice"));
        vm.SetPresence(Presence((aliceId, "alice")));

        _messenger.Send(new UserOfflineMessage(Guid.NewGuid(), aliceId));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.OnlineMembers);
    }

    [Fact]
    public void member_counts_match_their_respective_lists()
    {
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.SetMembers(Members("alice", "bob", "carol"));
        vm.SetPresence(Presence((aliceId, "alice"), (bobId, "bob")));

        Assert.Equal(2, vm.OnlineMemberCount);
        Assert.Equal(1, vm.OfflineMemberCount);
    }
}
