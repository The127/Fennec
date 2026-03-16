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
using Fennec.App.Themes;
using Fennec.App.ViewModels.Settings;
using Fennec.Client;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Fennec.Shared.Dtos.Auth;
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

public partial class MainAppViewModel : ObservableObject, IShortcutHandler, IRecipient<ServerCreatedMessage>, IRecipient<ServerJoinedMessage>, IRecipient<VoiceStateChangedMessage>, IRecipient<VoiceMuteToggledMessage>, IRecipient<VoiceDeafenToggledMessage>, IRecipient<ScreenShareStartedMessage>, IRecipient<ScreenShareStoppedMessage>, IRecipient<ScreenShareFrameMessage>, IRecipient<ScreenShareCursorMessage>, IRecipient<ScreenSharePopOutRequestedMessage>, IRecipient<ScreenSharePopOutClosedMessage>
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
    private readonly IMessageHubService _messageHubService;
    private readonly IVoiceCallService _voiceCallService;
    private readonly IAuthStore _authStore;

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
        ISettingsStore settingsStore,
        IMessageHubService messageHubService,
        IVoiceCallService voiceCallService,
        IVoiceHubService voiceHubService,
        IAuthStore authStore)
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
        _messageHubService = messageHubService;
        _voiceCallService = voiceCallService;
        _authStore = authStore;

        voiceHubService.Initialize();

        messenger.Register<ServerCreatedMessage>(this);
        messenger.Register<ServerJoinedMessage>(this);
        messenger.Register<VoiceStateChangedMessage>(this);
        messenger.Register<VoiceMuteToggledMessage>(this);
        messenger.Register<VoiceDeafenToggledMessage>(this);
        messenger.Register<ScreenShareStartedMessage>(this);
        messenger.Register<ScreenShareStoppedMessage>(this);
        messenger.Register<ScreenShareFrameMessage>(this);
        messenger.Register<ScreenShareCursorMessage>(this);
        messenger.Register<ScreenSharePopOutRequestedMessage>(this);
        messenger.Register<ScreenSharePopOutClosedMessage>(this);

        // Initialize voice state from service (handles case where VM is created after call started)
        IsInVoiceCall = voiceCallService.IsConnected;
        IsVoiceMuted = voiceCallService.IsMuted;
        IsVoiceDeafened = voiceCallService.IsDeafened;

        _routerField.Navigated += OnRouteNavigated;
    }

    private void OnRouteNavigated(object? sender, ObservableObject viewModel)
    {
        NavigateBackCommand.NotifyCanExecuteChanged();
        NavigateForwardCommand.NotifyCanExecuteChanged();
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

        UpdateFloatingScreenShareVisibility();
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

    public event Action? SearchFocusRequested;

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
                OpenSettingsCommand.Execute(null);
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
            case "nav.focusSearch":
                SearchFocusRequested?.Invoke();
                return true;
            case "nav.back":
                NavigateBackCommand.Execute(null);
                return true;
            case "nav.forward":
                NavigateForwardCommand.Execute(null);
                return true;
            case "app.zoomIn":
                ZoomInCommand.Execute(null);
                return true;
            case "app.zoomOut":
                ZoomOutCommand.Execute(null);
                return true;
            case "app.zoomReset":
                ZoomResetCommand.Execute(null);
                return true;
            case "voice.toggleMute":
                ToggleVoiceMuteCommand.Execute(null);
                return true;
            case "voice.toggleDeafen":
                ToggleVoiceDeafenCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private const double ZoomStep = 0.1;
    private const double ZoomMin = 0.5;
    private const double ZoomMax = 2.0;

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        CurrentThemeMode = AppThemes.ModeFromName(settings.ThemeMode);
        _messenger.Send(new ZoomChangedMessage(settings.ZoomLevel));

        if (settings.KeyBindings is { Count: > 0 })
            _keymapService.LoadOverrides(settings.KeyBindings);

        if (settings.MouseBindings is { Count: > 0 })
            _keymapService.LoadMouseOverrides(settings.MouseBindings);

        if (_session is not null)
        {
            try
            {
                var publicToken = await _client.Auth.GetPublicTokenAsync(
                    _session.Url,
                    new GetPublicTokenRequestDto { Audience = _session.Url });
                await _messageHubService.ConnectAsync(_session.Url, publicToken.Token);
            }
            catch (Exception ex)
            {
                // SignalR connection failure shouldn't block startup — messages still load via HTTP.
                _exceptionHandler.Handle(ex, "SignalR connection failed during startup");
            }
        }

        await NavigateToDashboardAsync();
        await LoadServersAsync(waitForRefresh: true);
    }

    [RelayCommand]
    private async Task ZoomInAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        var newZoom = Math.Min(settings.ZoomLevel + ZoomStep, ZoomMax);
        await ApplyZoomAsync(settings, newZoom);
    }

    [RelayCommand]
    private async Task ZoomOutAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        var newZoom = Math.Max(settings.ZoomLevel - ZoomStep, ZoomMin);
        await ApplyZoomAsync(settings, newZoom);
    }

    [RelayCommand]
    private async Task ZoomResetAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        await ApplyZoomAsync(settings, 1.0);
    }

    private async Task ApplyZoomAsync(AppSettings settings, double zoomLevel)
    {
        zoomLevel = Math.Round(zoomLevel, 1);
        settings.ZoomLevel = zoomLevel;
        await _settingsStore.SaveAsync(settings);
        _messenger.Send(new ZoomChangedMessage(zoomLevel));
        _toastManager.CreateToast($"Zoom: {zoomLevel:P0}")
            .WithDelay(2)
            .ShowInfo();
    }

    public void Receive(ServerCreatedMessage message)
    {
        _ = LoadServersAndNavigateToServerAsync(message.ServerId, message.ServerName);
    }

    private async Task LoadServersAndNavigateToServerAsync(Guid serverId, string serverName)
    {
        await LoadServersAsync(waitForRefresh: true);
        if (_client is null) return;
        var server = Servers.FirstOrDefault(s => s.Id == serverId);
        var instanceUrl = server?.InstanceUrl ?? _session!.Url;
        await _routerField.NavigateAsync(new ServerRoute(_client, _dialogManager, _serverStore, _messageHubService, _voiceCallService, _messenger, _toastManager, _settingsStore, serverId, serverName, instanceUrl, _session!.UserId, Username));
    }

    public void Receive(ServerJoinedMessage message)
    {
        _ = LoadServersAsync(waitForRefresh: true);
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

    private async Task LoadServersAsync(bool waitForRefresh = false)
    {
        if (_client is null || _session is null) return;

        var servers = await _serverStore.GetJoinedServersAsync(_session.Url, _client);
        UpdateServersList(servers);

        if (waitForRefresh)
        {
            await _serverStore.WaitForRefreshesAsync();
            var freshServers = await _serverStore.GetJoinedServersAsync(_session.Url, _client);
            UpdateServersList(freshServers);
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

    private bool CanNavigateBack => _routerField.CanGoBack;
    private bool CanNavigateForward => _routerField.CanGoForward;

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private async Task NavigateBackAsync()
    {
        await _routerField.NavigateBackAsync();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private async Task NavigateForwardAsync()
    {
        await _routerField.NavigateForwardAsync();
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
        await _routerField.NavigateAsync(new ServerRoute(_client, _dialogManager, _serverStore, _messageHubService, _voiceCallService, _messenger, _toastManager, _settingsStore, server.Id, server.Name, server.InstanceUrl, _session!.UserId, Username));
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightMode))]
    [NotifyPropertyChangedFor(nameof(IsAutoMode))]
    [NotifyPropertyChangedFor(nameof(IsDarkMode))]
    private Themes.ThemeMode _currentThemeMode = AppThemes.Auto;

    public bool IsLightMode => CurrentThemeMode == AppThemes.Light;
    public bool IsAutoMode => CurrentThemeMode == AppThemes.Auto;
    public bool IsDarkMode => CurrentThemeMode == AppThemes.Dark;

    [RelayCommand]
    private async Task SetThemeModeAsync(string modeName)
    {
        var mode = AppThemes.ModeFromName(modeName);
        var app = Application.Current!;
        var settings = await _settingsStore.LoadAsync();
        var palette = AppThemes.PaletteFromName(settings.Theme);

        ThemeVariant? osTheme = null;
        if (app.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            var ptv = desktop.MainWindow.PlatformSettings?.GetColorValues().ThemeVariant;
            if (ptv is not null)
                osTheme = ptv == Avalonia.Platform.PlatformThemeVariant.Light
                    ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        app.RequestedThemeVariant = AppThemes.Resolve(palette, mode, osTheme);
        CurrentThemeMode = mode;
        settings.Theme = palette.Name;
        settings.ThemeMode = mode.Name;
        await _settingsStore.SaveAsync(settings);
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        // Cycle: Light → Auto → Dark → Light
        var next = CurrentThemeMode == AppThemes.Light ? AppThemes.Auto
                 : CurrentThemeMode == AppThemes.Auto ? AppThemes.Dark
                 : AppThemes.Light;
        await SetThemeModeAsync(next.Name);
        _toastManager.CreateToast($"Mode: {next.Name}")
            .WithDelay(2)
            .ShowInfo();
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
    private async Task OpenSettingsAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        var vm = new SettingsViewModel(
            _messenger,
            _keymapService,
            _settingsStore,
            settings,
            Username,
            _session?.Url ?? "");
        _dialogManager.CreateDialog(vm)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void SwitchAccount()
    {
        var vm = new SwitchAccountViewModel(_dialogManager, _authStore, _session?.UserId);
        _dialogManager.CreateDialog(vm)
            .Dismissible()
            .WithSuccessCallback<SwitchAccountViewModel>(ctx =>
            {
                if (ctx.LoginRequested)
                {
                    _messenger.Send(new UserLoggedOutMessage());
                    return Task.CompletedTask;
                }

                if (ctx.SelectedSession is not null)
                {
                    return SwitchToAccountAsync(ctx.SelectedSession);
                }

                return Task.CompletedTask;
            })
            .Show();
    }

    // --- Global voice state ---

    [ObservableProperty]
    private bool _isInVoiceCall;

    [ObservableProperty]
    private string? _voiceChannelName;

    [ObservableProperty]
    private string? _voiceServerName;

    [ObservableProperty]
    private bool _isVoiceMuted;

    [ObservableProperty]
    private bool _isVoiceDeafened;

    private Guid? _voiceServerId;

    public void Receive(VoiceStateChangedMessage message)
    {
        IsInVoiceCall = message.IsConnected;
        _voiceServerId = message.ServerId;

        if (message.IsConnected && message.ServerId is not null)
        {
            var server = Servers.FirstOrDefault(s => s.Id == message.ServerId);
            VoiceServerName = server?.Name;
            // Channel name will be resolved from the server's channel list isn't available here;
            // We don't have channel data at this level, so leave it as the server name.
            VoiceChannelName = null;
        }
        else
        {
            VoiceServerName = null;
            VoiceChannelName = null;
            IsVoiceMuted = false;
            IsVoiceDeafened = false;

            // Reset floating screen share
            _activeScreenShareCount = 0;
            _focusedScreenShareUserId = null;
            FloatingScreenShareFrame = null;
            FloatingSharerUsername = null;
            CloseAllPopOutWindows();
            UpdateFloatingScreenShareVisibility();
        }
    }

    public void Receive(VoiceMuteToggledMessage message)
    {
        IsVoiceMuted = message.IsMuted;
    }

    public void Receive(VoiceDeafenToggledMessage message)
    {
        IsVoiceDeafened = message.IsDeafened;
        if (message.IsDeafened)
            IsVoiceMuted = true;
    }

    [RelayCommand]
    private void ToggleVoiceMute()
    {
        if (!_voiceCallService.IsConnected) return;
        var newMuted = !_voiceCallService.IsMuted;
        _voiceCallService.SetMuted(newMuted);
        _messenger.Send(new VoiceMuteToggledMessage(newMuted));
    }

    [RelayCommand]
    private void ToggleVoiceDeafen()
    {
        if (!_voiceCallService.IsConnected) return;
        var newDeafened = !_voiceCallService.IsDeafened;
        _voiceCallService.SetDeafened(newDeafened);
        _messenger.Send(new VoiceDeafenToggledMessage(newDeafened));
    }

    [RelayCommand]
    private async Task LeaveVoiceCallAsync()
    {
        await _voiceCallService.LeaveAsync();
    }

    [RelayCommand]
    private async Task NavigateToVoiceChannelAsync()
    {
        if (_voiceServerId is null || _client is null || _session is null) return;
        var server = Servers.FirstOrDefault(s => s.Id == _voiceServerId);
        if (server is null) return;
        await _routerField.NavigateAsync(new ServerRoute(_client, _dialogManager, _serverStore, _messageHubService, _voiceCallService, _messenger, _toastManager, _settingsStore, server.Id, server.Name, server.InstanceUrl, _session.UserId, Username));
    }

    // --- Floating screen share ---

    private int _activeScreenShareCount;

    [ObservableProperty]
    private bool _showFloatingScreenShare;

    [ObservableProperty]
    private WriteableBitmap? _floatingScreenShareFrame;

    [ObservableProperty]
    private double _floatingCursorX;

    [ObservableProperty]
    private double _floatingCursorY;

    [ObservableProperty]
    private CursorType _floatingCursorShape;

    [ObservableProperty]
    private string? _floatingSharerUsername;

    private Guid? _focusedScreenShareUserId;

    private void UpdateFloatingScreenShareVisibility()
    {
        var isOnVoiceServer = _routerField.CurrentViewModel is ServerViewModel svm
                              && svm.ServerId == _voiceServerId;
        ShowFloatingScreenShare = _activeScreenShareCount > 0 && !isOnVoiceServer && !IsScreenSharePoppedOut;
    }

    public void Receive(ScreenShareStartedMessage message)
    {
        if (message.ServerId != _voiceServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _activeScreenShareCount++;
            _focusedScreenShareUserId ??= message.UserId;
            if (_focusedScreenShareUserId == message.UserId)
                FloatingSharerUsername = message.Username;
            UpdateFloatingScreenShareVisibility();
        });
    }

    public void Receive(ScreenShareStoppedMessage message)
    {
        if (message.ServerId != _voiceServerId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _activeScreenShareCount = Math.Max(0, _activeScreenShareCount - 1);

            if (_focusedScreenShareUserId == message.UserId)
            {
                _focusedScreenShareUserId = null;
                FloatingSharerUsername = null;
                FloatingScreenShareFrame = null;
            }

            if (_activeScreenShareCount == 0)
            {
                FloatingScreenShareFrame = null;
                FloatingSharerUsername = null;
            }

            UpdateFloatingScreenShareVisibility();
        });
    }

    public void Receive(ScreenShareFrameMessage message)
    {
        if (_focusedScreenShareUserId != message.UserId) return;
        if (!ShowFloatingScreenShare) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (FloatingScreenShareFrame is null
                    || FloatingScreenShareFrame.PixelSize.Width != message.Width
                    || FloatingScreenShareFrame.PixelSize.Height != message.Height)
                {
                    FloatingScreenShareFrame = new WriteableBitmap(
                        new Avalonia.PixelSize(message.Width, message.Height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Rgba8888,
                        Avalonia.Platform.AlphaFormat.Unpremul);
                }

                using (var frameBuffer = FloatingScreenShareFrame.Lock())
                {
                    var srcStride = message.Width * 4;
                    var dstStride = frameBuffer.RowBytes;

                    if (srcStride == dstStride)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            message.RgbaData, 0, frameBuffer.Address, message.RgbaData.Length);
                    }
                    else
                    {
                        for (int y = 0; y < message.Height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(
                                message.RgbaData, y * srcStride,
                                frameBuffer.Address + y * dstStride, srcStride);
                        }
                    }
                }

                OnPropertyChanged(nameof(FloatingScreenShareFrame));
            }
            catch
            {
                // Ignore render failures
            }
        });
    }

    public void Receive(ScreenShareCursorMessage message)
    {
        if (_focusedScreenShareUserId != message.UserId) return;
        if (!ShowFloatingScreenShare) return;

        Dispatcher.UIThread.Post(() =>
        {
            FloatingCursorX = message.X;
            FloatingCursorY = message.Y;
            FloatingCursorShape = message.Type;
        });
    }

    // --- Screen share pop-out ---

    private readonly Dictionary<Guid, Views.ScreenShareWindow> _popOutWindows = new();

    [ObservableProperty]
    private bool _isScreenSharePoppedOut;

    public void Receive(ScreenSharePopOutRequestedMessage message)
    {
        Dispatcher.UIThread.Post(() => OpenPopOutWindow(message.UserId, message.Username));
    }

    public void Receive(ScreenSharePopOutClosedMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _popOutWindows.Remove(message.UserId);
            if (_popOutWindows.Count == 0)
                IsScreenSharePoppedOut = false;
            UpdateFloatingScreenShareVisibility();
        });
    }

    [RelayCommand]
    private void PopOutFloatingScreenShare()
    {
        if (_focusedScreenShareUserId is null || FloatingSharerUsername is null) return;
        OpenPopOutWindow(_focusedScreenShareUserId.Value, FloatingSharerUsername);
    }

    private void OpenPopOutWindow(Guid userId, string username)
    {
        if (_popOutWindows.ContainsKey(userId))
        {
            _popOutWindows[userId].Activate();
            return;
        }

        if (_voiceServerId is null) return;

        var vm = new ScreenShareWindowViewModel(_messenger, _voiceCallService, userId, username, _voiceServerId.Value);
        var window = new Views.ScreenShareWindow { DataContext = vm };
        _popOutWindows[userId] = window;
        IsScreenSharePoppedOut = true;
        UpdateFloatingScreenShareVisibility();
        window.Show();
    }

    private void CloseAllPopOutWindows()
    {
        foreach (var window in _popOutWindows.Values.ToList())
            window.Close();
        _popOutWindows.Clear();
        IsScreenSharePoppedOut = false;
    }

    [RelayCommand]
    private async Task NavigateToFloatingScreenShareAsync()
    {
        await NavigateToVoiceChannelAsync();
    }

    private async Task SwitchToAccountAsync(AuthSession session)
    {
        await _messageHubService.DisconnectAsync();
        await _authService.SwitchAccountAsync(session);
        _messenger.Send(new LoginSucceededMessage(session));
    }

    [RelayCommand]
    private async Task Logout(CancellationToken cancellationToken)
    {
        await _messageHubService.DisconnectAsync();
        await _authService.LogoutAsync(cancellationToken);
        _messenger.Send(new UserLoggedOutMessage());
    }
}
