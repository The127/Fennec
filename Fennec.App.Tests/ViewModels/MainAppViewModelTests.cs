using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Exceptions;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Shortcuts;
using Fennec.App.ViewModels;
using Fennec.Client;
using Fennec.Client.Clients;
using Fennec.Shared.Dtos.Server;
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
    private readonly IServerStore _serverStore = Substitute.For<IServerStore>();
    private readonly IKeymapService _keymapService = new KeymapService();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IMessageHubService _messageHubService = Substitute.For<IMessageHubService>();

    public MainAppViewModelTests()
    {
        _clientFactory.Create().Returns(_client);
        _client.Server.Returns(_serverClient);
        _serverClient.ListJoinedServersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ListJoinedServersResponseDto { Servers = [] });
        _serverStore.GetJoinedServersAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ListJoinedServersResponseItemDto>()));
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings());
    }

    private MainAppViewModel CreateViewModel()
    {
        var vm = new MainAppViewModel(_router, _messenger, _authService, _clientFactory, new ToastManager(), NullExceptionHandler.Instance, new DialogManager(), _serverStore, _keymapService, _settingsStore, _messageHubService);
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
        _serverStore.ClearReceivedCalls();

        _messenger.Send(new ServerJoinedMessage());

        _serverStore.Received().GetJoinedServersAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task LoadServersAsync_Should_Not_Result_In_Duplicate_Servers()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var servers = new List<ListJoinedServersResponseItemDto>
        {
            new() { Id = serverId, Name = "Server 1", InstanceUrl = "https://1.fennec.chat" }
        };
        
        // Mock server store to return the server
        _serverStore.GetJoinedServersAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ListJoinedServersResponseItemDto>(servers)));
            
        // Mock API to return the same server
        _serverClient.ListJoinedServersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ListJoinedServersResponseDto { Servers = new List<ListJoinedServersResponseItemDto>(servers) });
            
        var vm = CreateViewModel();

        // Act
        // LoadServersAsync is private, but it's called by InitializeAsync
        await vm.InitializeAsync();

        // Assert
        Assert.Single(vm.Servers);
        Assert.Equal(serverId, vm.Servers[0].Id);
    }

    [Fact]
    public async Task UpdateServersList_DuplicateResponse_Should_Handle_Gracefully()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var serversWithDuplicates = new List<ListJoinedServersResponseItemDto>
        {
            new() { Id = serverId, Name = "Server 1", InstanceUrl = "https://1.fennec.chat" },
            new() { Id = serverId, Name = "Server 1", InstanceUrl = "https://1.fennec.chat" }
        };
        
        _serverStore.GetJoinedServersAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(serversWithDuplicates));
            
        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        Assert.Single(vm.Servers);
    }
    [Fact]
    public async Task Concurrent_LoadServersAsync_Should_Not_Result_In_Duplicate_Servers()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var servers = new List<ListJoinedServersResponseItemDto>
        {
            new() { Id = serverId, Name = "Server 1", InstanceUrl = "https://1.fennec.chat" }
        };
        
        _serverStore.GetJoinedServersAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ListJoinedServersResponseItemDto>(servers)));
            
        _serverClient.ListJoinedServersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => 
            {
                await Task.Delay(10); // Simulate network delay
                return new ListJoinedServersResponseDto { Servers = new List<ListJoinedServersResponseItemDto>(servers) };
            });
            
        var vm = CreateViewModel();

        // Act
        var task1 = vm.InitializeAsync();
        var task2 = vm.InitializeAsync();

        await Task.WhenAll(task1, task2);

        // Assert
        Assert.Single(vm.Servers);
    }
}
