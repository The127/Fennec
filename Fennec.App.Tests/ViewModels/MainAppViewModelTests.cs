using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.Services.Auth;
using Fennec.App.ViewModels;
using Fennec.Client;
using Fennec.Client.Clients;
using Fennec.Shared.Dtos.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class MainAppViewModelTests
{
    private readonly IRouter _router = Substitute.For<IRouter>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly IClientFactory _clientFactory = Substitute.For<IClientFactory>();
    private readonly IFennecClient _client = Substitute.For<IFennecClient>();
    private readonly IServerClient _serverClient = Substitute.For<IServerClient>();

    public MainAppViewModelTests()
    {
        _clientFactory.Create().Returns(_client);
        _client.Server.Returns(_serverClient);
        _serverClient.ListJoinedServersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ListJoinedServersResponseDto { Servers = [] });
    }

    private MainAppViewModel CreateViewModel()
    {
        var vm = new MainAppViewModel(_router, _messenger, _authService, _clientFactory, new ToastManager(), NullLogger<MainAppViewModel>.Instance);
        vm.ApplySession(new AuthSession
        {
            Username = "alice",
            Url = "https://fennec.chat",
            SessionToken = "token",
            UserId = Guid.NewGuid(),
        });
        return vm;
    }

    [Fact]
    public async Task Logging_out_calls_the_auth_service()
    {
        var vm = CreateViewModel();

        await vm.LogoutCommand.ExecuteAsync(null);

        await _authService.Received(1).LogoutAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Server_list_refreshes_after_server_joined()
    {
        var vm = CreateViewModel();
        _serverClient.ClearReceivedCalls();

        _messenger.Send(new ServerJoinedMessage());

        _serverClient.Received().ListJoinedServersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_invite_link_navigates_to_create_invite_view()
    {
        var serverId = Guid.NewGuid();
        var vm = CreateViewModel();
        var server = new SidebarServer(serverId, "Test", "fennec.chat");

        await vm.CreateInviteLinkCommand.ExecuteAsync(server);

        await _router.Received().NavigateAsync(Arg.Any<IRoute>());
    }
}
