using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Exceptions;
using Fennec.App.Messages;
using Fennec.App.Routes;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Shortcuts;
using Fennec.App.ViewModels.Settings;
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

public partial class MainAppViewModel : ObservableObject, IShortcutHandler, IRecipient<ServerCreatedMessage>, IRecipient<ServerJoinedMessage>
{
    private readonly IRouter _routerField;
    private readonly IMessenger _messenger;
    private readonly IAuthService _authService;
    private readonly IClientFactory _clientFactory;
    private readonly ToastManager _toastManager;
    private readonly IExceptionHandler _exceptionHandler;
    private readonly DialogManager _dialogManager;
    private readonly IServerStore _serverStore;
    private readonly IKeymapService _keymapService;

    public MainAppViewModel(
        IRouter router,
        IMessenger messenger,
        IAuthService authService,
        IClientFactory clientFactory,
        ToastManager toastManager,
        IExceptionHandler exceptionHandler,
        DialogManager dialogManager,
        IServerStore serverStore,
        IKeymapService keymapService)
    {
        _routerField = router;
        _router = router;
        _messenger = messenger;
        _authService = authService;
        _clientFactory = clientFactory;
        _toastManager = toastManager;
        _exceptionHandler = exceptionHandler;
        _dialogManager = dialogManager;
        _serverStore = serverStore;
        _keymapService = keymapService;

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

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private SettingsViewModel? _settingsViewModel;

    public ObservableCollection<SidebarServer> Servers { get; } = [];

    public ShortcutContext ShortcutContext => ShortcutContext.MainApp;

    public bool HandleShortcut(string shortcutId)
    {
        // When settings is open, only allow closing or toggling theme
        if (IsSettingsOpen)
        {
            switch (shortcutId)
            {
                case "app.toggleTheme":
                    ToggleTheme();
                    return true;
                case "app.openSettings":
                    CloseSettings();
                    return true;
                default:
                    return false;
            }
        }

        switch (shortcutId)
        {
            case "app.toggleTheme":
                ToggleTheme();
                return true;
            case "app.openSettings":
                OpenSettings();
                return true;
            case "nav.dashboard":
                NavigateToDashboardCommand.Execute(null);
                return true;
            case "nav.friends":
                NavigateToFriendsCommand.Execute(null);
                return true;
            case "nav.calls":
                NavigateToCallsCommand.Execute(null);
                return true;
            case "nav.add":
                NavigateToAddCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    public async Task InitializeAsync()
    {
        await NavigateToDashboardAsync();
        await LoadServersAsync();
    }

    public void Receive(ServerCreatedMessage message)
    {
        _ = LoadServersAndNavigateToServerAsync(message.ServerId, message.ServerName);
    }

    private async Task LoadServersAndNavigateToServerAsync(Guid serverId, string serverName)
    {
        await LoadServersAsync();
        if (_client is null) return;
        await _routerField.NavigateAsync(new ServerRoute(_client, _dialogManager, _serverStore, serverId, serverName, _session!.Url));
    }

    public void Receive(ServerJoinedMessage message)
    {
        _ = LoadServersAsync();
    }

    public void ApplySession(AuthSession session)
    {
        _session = session;
        _client = _clientFactory.Create();
        _client.SetHomeSession(session.Url, session.SessionToken);
        Username = session.Username;
        UserAtServer = $"{session.Username}@{session.Url}";
        AvatarFallback = session.Username[..1].ToUpperInvariant();
    }

    private async Task LoadServersAsync()
    {
        if (_client is null || _session is null) return;

        var storedServers = await _serverStore.GetJoinedServersAsync();
        UpdateServersList(storedServers);

        try
        {
            var response = await _client.Server.ListJoinedServersAsync(_session.Url);
            await _serverStore.SetJoinedServersAsync(response.Servers);
            UpdateServersList(response.Servers);
        }
        catch (Exception ex)
        {
            _exceptionHandler.Handle(ex, "Failed to load servers for user {User} on {Url}", UserAtServer, _session.Url);
        }
    }

    private void UpdateServersList(List<ListJoinedServersResponseItemDto>? servers)
    {
        if (servers == null) return;
        
        var newServers = servers.DistinctBy(s => s.Id).ToList();
        
        // Basic reconciliation to reduce flicker and handle concurrent updates more gracefully.
        // Remove ones not in newServers.
        var newServerIds = newServers.Select(s => s.Id).ToHashSet();
        for (int i = Servers.Count - 1; i >= 0; i--)
        {
            if (!newServerIds.Contains(Servers[i].Id))
            {
                Servers.RemoveAt(i);
            }
        }

        // Add ones not in Servers.
        var currentServerIds = Servers.Select(s => s.Id).ToHashSet();
        foreach (var server in newServers)
        {
            if (!currentServerIds.Contains(server.Id))
            {
                Servers.Add(new SidebarServer(server.Id, server.Name, server.InstanceUrl));
            }
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
        await _routerField.NavigateAsync(new ServerRoute(_client, _dialogManager, _serverStore, server.Id, server.Name, server.InstanceUrl));
    }

    [RelayCommand]
    private async Task NavigateToAddAsync()
    {
        if (_client is null || _session is null) return;
        await _routerField.NavigateAsync(new AddRoute(_routerField, _client, _messenger, _session.Url));
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
        await _routerField.NavigateAsync(new CreateInviteRoute(_client, _routerField, _toastManager, server.Id, server.InstanceUrl));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (IsSettingsOpen) return;
        SettingsViewModel = new SettingsViewModel(
            _keymapService,
            Username,
            _session?.Url ?? "",
            CloseSettings);
        IsSettingsOpen = true;
    }

    private void CloseSettings()
    {
        IsSettingsOpen = false;
        SettingsViewModel = null;
    }

    [RelayCommand]
    private async Task Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(cancellationToken);
        _messenger.Send(new UserLoggedOutMessage());
    }
}
