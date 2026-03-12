using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;
using Fennec.Client.Clients;
using Fennec.Shared.Dtos.Server;
using NSubstitute;

namespace Fennec.App.Tests.ViewModels;

public class JoinServerViewModelTests
{
    private readonly IFennecClient _client = Substitute.For<IFennecClient>();
    private readonly IServerClient _serverClient = Substitute.For<IServerClient>();
    private readonly IRouter _router = Substitute.For<IRouter>();
    private readonly WeakReferenceMessenger _messenger = new();

    public JoinServerViewModelTests()
    {
        _client.Server.Returns(_serverClient);
        _client.BaseAddress.Returns("https://my.fennec.chat");
    }

    private JoinServerViewModel CreateViewModel() => new(_client, _router, _messenger);

    [Fact]
    public async Task Valid_invite_link_joins_server_and_navigates_back()
    {
        var vm = CreateViewModel();
        vm.InviteLink = "https://fennec.chat/invite/aBcD1234";

        await vm.JoinServerCommand.ExecuteAsync(null);

        await _serverClient.Received().JoinServerAsync(
            Arg.Is<JoinServerRequestDto>(r =>
                r.InviteCode == "aBcD1234" &&
                r.InstanceUrl == "fennec.chat"),
            Arg.Any<CancellationToken>());
        await _router.Received().NavigateBackAsync();
    }

    [Fact]
    public async Task Valid_invite_link_sends_server_joined_message()
    {
        var vm = CreateViewModel();
        vm.InviteLink = "https://fennec.chat/invite/aBcD1234";
        var messageReceived = false;
        _messenger.Register<ServerJoinedMessage>(this, (_, _) => messageReceived = true);

        await vm.JoinServerCommand.ExecuteAsync(null);

        Assert.True(messageReceived);
    }

    [Fact]
    public async Task Invalid_link_format_shows_error()
    {
        var vm = CreateViewModel();
        vm.InviteLink = "not-a-url";

        await vm.JoinServerCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        await _serverClient.DidNotReceive().JoinServerAsync(
            Arg.Any<JoinServerRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Link_without_invite_path_shows_error()
    {
        var vm = CreateViewModel();
        vm.InviteLink = "https://fennec.chat/other/thing";

        await vm.JoinServerCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        await _serverClient.DidNotReceive().JoinServerAsync(
            Arg.Any<JoinServerRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_link_shows_error()
    {
        var vm = CreateViewModel();
        vm.InviteLink = "";

        await vm.JoinServerCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        await _serverClient.DidNotReceive().JoinServerAsync(
            Arg.Any<JoinServerRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Api_failure_shows_error_message()
    {
        var vm = CreateViewModel();
        vm.InviteLink = "https://fennec.chat/invite/aBcD1234";
        _serverClient.JoinServerAsync(Arg.Any<JoinServerRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Server error")));

        await vm.JoinServerCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        await _router.DidNotReceive().NavigateBackAsync();
    }
}
