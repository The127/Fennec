using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Services.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel
    : ObservableRecipient, IRecipient<LoginSucceededMessage>, IRecipient<UserLoggedOutMessage>, IRecipient<ZoomChangedMessage>
{
    [ObservableProperty]
    private ObservableObject _currentViewModel;

    [ObservableProperty]
    private AppShellState _state = AppShellState.Loading;

    [ObservableProperty]
    private ToastManager _toastManager;

    [ObservableProperty]
    private DialogManager _dialogManager;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private UpdateInfo? _availableUpdate;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private double _updateProgress;

    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthStore _authStore;
    private readonly IDbPathProvider _dbPathProvider;
    private readonly IUpdateService _updateService;
    private readonly ILogger<AppShellViewModel> _logger;

    public bool IsUpdateAvailable => AvailableUpdate is not null;

    public AppShellViewModel(
        IServiceProvider serviceProvider,
        IAuthStore authStore,
        IDbPathProvider dbPathProvider,
        ToastManager toastManager,
        DialogManager dialogManager,
        IMessenger messenger,
        IUpdateService updateService,
        ILogger<AppShellViewModel>? logger = null
    ) : base(messenger)
    {
        _serviceProvider = serviceProvider;
        _authStore = authStore;
        _dbPathProvider = dbPathProvider;
        _toastManager = toastManager;
        _dialogManager = dialogManager;
        _updateService = updateService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AppShellViewModel>.Instance;
        _currentViewModel = ActivatorUtilities.CreateInstance<LoadingViewModel>(_serviceProvider);

        Messenger.RegisterAll(this);
    }

    public bool IsLoggedIn => State == AppShellState.LoggedIn;
    public bool IsLoggedOut => State == AppShellState.LoggedOut;

    public async Task InitializeAsync()
    {
        var loadingVm = _currentViewModel as LoadingViewModel;
        bool updateDownloaded = false;

        if (loadingVm is not null)
            loadingVm.Status = "Checking for updates\u2026";

        _logger.LogInformation("Checking for updates");
        var update = await _updateService.CheckForUpdateAsync();

        if (update is not null)
        {
            _logger.LogInformation("Update available: v{Version} — downloading", update.Version);
            if (loadingVm is not null)
            {
                loadingVm.IsUpdating = true;
                loadingVm.Status = $"Downloading update v{update.Version}\u2026";
            }

            var progress = new Progress<double>(p =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (loadingVm is not null) loadingVm.UpdateProgress = p;
                }));

            try
            {
                await _updateService.DownloadAndApplyAsync(update, progress);
                updateDownloaded = true;
                _logger.LogInformation("Update applied, restarting");
                // App restarts inside DownloadAndApplyAsync; code below is unreachable on success.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update download failed, falling back to banner");
                if (loadingVm is not null)
                {
                    loadingVm.IsUpdating = false;
                    loadingVm.Status = string.Empty;
                }
                AvailableUpdate = update;
                OnPropertyChanged(nameof(IsUpdateAvailable));
            }
        }
        else
        {
            _logger.LogInformation("No update found");
            if (loadingVm is not null)
                loadingVm.Status = string.Empty;
        }

        if (!updateDownloaded)
            await Task.Delay(1500);

        var currentSession = await _authStore.GetCurrentAuthSessionAsync();
        if (currentSession is not null)
        {
            _dbPathProvider.CurrentDbPath = _dbPathProvider.GetDbPath(currentSession.UserId);
            EnsureDatabase();
            var vm = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider, Messenger);
            vm.ApplySession(currentSession);
            CurrentViewModel = vm;
            State = AppShellState.LoggedIn;
            await vm.InitializeAsync();
        }
        else
        {
            var autoLogin = Environment.GetEnvironmentVariable("FENNEC_AUTO_LOGIN");
            var autoPassword = Environment.GetEnvironmentVariable("FENNEC_AUTO_LOGIN_PASSWORD");
            if (autoLogin is not null && autoPassword is not null && FederatedAddress.TryParse(autoLogin, out var autoAddress))
            {
                var username = autoAddress!.Username;
                var instanceUrl = autoAddress.InstanceUrl!;
                try
                {
                    var authService = _serviceProvider.GetRequiredService<IAuthService>();
                    var session = await authService.LoginAsync(username, autoPassword, instanceUrl, CancellationToken.None);
                    if (session is not null)
                    {
                        _dbPathProvider.CurrentDbPath = _dbPathProvider.GetDbPath(session.UserId);
                        EnsureDatabase();
                        var vm = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider, Messenger);
                        vm.ApplySession(session);
                        CurrentViewModel = vm;
                        State = AppShellState.LoggedIn;
                        await vm.InitializeAsync();
                        return;
                    }
                    // auto-login returned null
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-login failed for {Username}@{InstanceUrl}", username, instanceUrl);
                }
            }

            CurrentViewModel = ActivatorUtilities.CreateInstance<AuthViewModel>(_serviceProvider);
            State = AppShellState.LoggedOut;
        }
    }

    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        if (AvailableUpdate is null || IsUpdating) return;

        IsUpdating = true;
        UpdateProgress = 0;

        var progress = new Progress<double>(p =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateProgress = p));

        try
        {
            await _updateService.DownloadAndApplyAsync(AvailableUpdate, progress);
        }
        catch (Exception)
        {
            IsUpdating = false;
            _toastManager.CreateToast("Update failed")
                .WithContent("Could not download the update. Please try again later.")
                .WithDelay(5)
                .ShowError();
        }
    }

    public void Receive(LoginSucceededMessage message)
    {
        EnsureDatabase();
        var vm = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider, Messenger);
        vm.ApplySession(message.Session);
        CurrentViewModel = vm;
        State = AppShellState.LoggedIn;
        _ = vm.InitializeAsync();
    }

    public void Receive(UserLoggedOutMessage message)
    {
        CurrentViewModel = ActivatorUtilities.CreateInstance<AuthViewModel>(_serviceProvider);
        State = AppShellState.LoggedOut;
    }

    private void EnsureDatabase()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            db.Database.EnsureCreated();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        }
    }

    public void Receive(ZoomChangedMessage message)
    {
        ZoomLevel = message.ZoomLevel;
    }
}
