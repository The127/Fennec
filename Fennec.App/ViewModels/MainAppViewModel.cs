using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Routes;
using Fennec.App.Routing;
using Fennec.App.Services.Auth;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class SidebarServer(Guid id, string name, string instanceUrl) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string InstanceUrl { get; } = instanceUrl;
    public string AvatarFallback { get; } = name[..1].ToUpperInvariant();
}

public partial class MainAppViewModel : ObservableObject, IRecipient<ServerCreatedMessage>, IRecipient<ServerJoinedMessage>
{
    private readonly IRouter _routerField;
    private readonly IMessenger _messenger;
    private readonly IAuthService _authService;
    private readonly IClientFactory _clientFactory;
    private readonly ToastManager _toastManager;

    public MainAppViewModel(IRouter router, IMessenger messenger, IAuthService authService, IClientFactory clientFactory, ToastManager toastManager)
    {
        _routerField = router;
        _router = router;
        _messenger = messenger;
        _authService = authService;
        _clientFactory = clientFactory;
        _toastManager = toastManager;

        messenger.Register<ServerCreatedMessage>(this);
        messenger.Register<ServerJoinedMessage>(this);
    }

    [ObservableProperty]
    private IRouter _router;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _userAtServer = string.Empty;

    [ObservableProperty]
    private string _avatarFallback = string.Empty;

    private AuthSession? _session;
    private IFennecClient? _client;

    public ObservableCollection<SidebarServer> Servers { get; } = [];

    public async Task InitializeAsync()
    {
        await NavigateToDashboardAsync();
        await AcquireBearerTokenAsync();
        await LoadServersAsync();
    }

    public void Receive(ServerCreatedMessage message)
    {
        _ = LoadServersAsync();
    }

    public void Receive(ServerJoinedMessage message)
    {
        _ = LoadServersAsync();
    }

    public void ApplySession(AuthSession session)
    {
        _session = session;
        _client = _clientFactory.Create(session.Url, session.SessionToken);
        Username = session.Username;
        UserAtServer = $"{session.Username}@{session.Url}";
        AvatarFallback = session.Username[..1].ToUpperInvariant();
    }

    private async Task AcquireBearerTokenAsync()
    {
        if (_client is null || _session is null) return;

        try
        {
            var response = await _client.Auth.GetPublicTokenAsync(new Fennec.Shared.Dtos.Auth.GetPublicTokenRequestDto
            {
                Audience = $"https://{_session.Url}",
            });
            _client.SetBearerToken(response.Token);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Failed to acquire bearer token")
                .WithContent(ex.Message)
                .WithDelay(5)
                .ShowError();
        }
    }

    private async Task LoadServersAsync()
    {
        if (_client is null) return;

        try
        {
            var response = await _client.Server.ListJoinedServersAsync();

            Servers.Clear();
            foreach (var server in response.Servers)
            {
                Servers.Add(new SidebarServer(server.Id, server.Name, server.InstanceUrl));
            }
        }
        catch
        {
            // Server unreachable — sidebar stays empty.
        }
    }

    [RelayCommand]
    private async Task NavigateToDashboardAsync()
    {
        await _routerField.NavigateAsync(new DashboardRoute());
    }

    [RelayCommand]
    private async Task NavigateToFriendsAsync()
    {
        await _routerField.NavigateAsync(new FriendsRoute());
    }

    [RelayCommand]
    private async Task NavigateToServerAsync(SidebarServer server)
    {
        if (_client is null) return;
        await _routerField.NavigateAsync(new ServerRoute(_client, server.Id, server.Name));
    }

    [RelayCommand]
    private async Task NavigateToAddAsync()
    {
        if (_client is null) return;
        await _routerField.NavigateAsync(new AddRoute(_routerField, _client, _messenger));
    }

    [RelayCommand]
    private async Task NavigateToCallsAsync()
    {
        await _routerField.NavigateAsync(new CallsRoute());
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }

    [RelayCommand]
    private async Task CreateInviteLinkAsync(SidebarServer server)
    {
        if (_client is null || _session is null) return;
        await _routerField.NavigateAsync(new CreateInviteRoute(_client, _routerField, _toastManager, server.Id, _session.Url));
    }

    [RelayCommand]
    private async Task Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(cancellationToken);
        _messenger.Send(new UserLoggedOutMessage());
    }
}
