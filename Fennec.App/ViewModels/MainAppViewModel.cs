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
    private readonly ISettingsStore _settingsStore;

    public MainAppViewModel(
        IRouter router,
        IMessenger messenger,
        IAuthService authService,
        IClientFactory clientFactory,
        ToastManager toastManager,
        IExceptionHandler exceptionHandler,
        DialogManager dialogManager,
        IServerStore serverStore,
        IKeymapService keymapService,
        ISettingsStore settingsStore)
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
        _settingsStore = settingsStore;

        messenger.Register<ServerCreatedMessage>(this);
        messenger.Register<ServerJoinedMessage>(this);

        _routerField.Navigated += OnRouteNavigated;
    }

    private void OnRouteNavigated(object? sender, ObservableObject viewModel)
    {
        // Clear search on the previous route
        if (_currentSearchableRoute is not null)
        {
            _currentSearchableRoute.ClearSearch();
            _currentSearchableRoute = null;
        }

        SearchText = string.Empty;

        if (viewModel is ISearchableRoute searchable)
        {
            _currentSearchableRoute = searchable;
            SearchWatermark = searchable.SearchWatermark;
            IsSearchable = true;
        }
        else
        {
            SearchWatermark = "Search...";
            IsSearchable = false;
        }
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
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _searchWatermark = "Search...";

    [ObservableProperty]
    private bool _isSearchable;

    private ISearchableRoute? _currentSearchableRoute;

    partial void OnSearchTextChanged(string value)
    {
        _currentSearchableRoute?.ApplySearch(value);
    }

    public ObservableCollection<SidebarServer> Servers { get; } = [];

    public ShortcutContext ShortcutContext => ShortcutContext.MainApp;

    public bool HandleShortcut(string shortcutId)
    {
        switch (shortcutId)
        {
            case "app.toggleTheme":
                ToggleThemeCommand.Execute(null);
                return true;
            case "app.openSettings":
                OpenSettings();
                return true;
            case "nav.quickNav":
                OpenQuickNavCommand.Execute(null);
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
    private async Task ToggleThemeAsync()
    {
        var app = Application.Current!;
        var newTheme = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
        app.RequestedThemeVariant = newTheme;
        await _settingsStore.SaveAsync(new AppSettings { Theme = newTheme == ThemeVariant.Dark ? "Dark" : "Light" });
    }

    [RelayCommand]
    private async Task CreateInviteLinkAsync(SidebarServer server)
    {
        if (_client is null || _session is null) return;
        await _routerField.NavigateAsync(new CreateInviteRoute(_client, _routerField, _toastManager, server.Id, server.InstanceUrl));
    }

    [RelayCommand]
    private void OpenQuickNav()
    {
        var items = new List<QuickNavItem>
        {
            new("Dashboard", "Navigation", "\ud83c\udfe0", () => NavigateToDashboardCommand.Execute(null)),
            new("Friends", "Navigation", "\ud83d\udc65", () => NavigateToFriendsCommand.Execute(null)),
            new("Calls", "Navigation", "\ud83d\udcde", () => NavigateToCallsCommand.Execute(null)),
            new("Add / Join Server", "Navigation", "\u2795", () => NavigateToAddCommand.Execute(null)),
        };

        foreach (var server in Servers)
        {
            var s = server;
            items.Add(new QuickNavItem(s.Name, "Server", "\ud83d\udda5\ufe0f", () => NavigateToServerCommand.Execute(s)));
        }

        var vm = new QuickNavDialogViewModel(_dialogManager, items);
        _dialogManager.CreateDialog(vm)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(
            _keymapService,
            _settingsStore,
            Username,
            _session?.Url ?? "");
        _dialogManager.CreateDialog(vm)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(cancellationToken);
        _messenger.Send(new UserLoggedOutMessage());
    }
}
