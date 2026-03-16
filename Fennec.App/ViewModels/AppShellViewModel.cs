using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Services.Storage;
using Microsoft.Extensions.DependencyInjection;
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

    public bool IsUpdateAvailable => AvailableUpdate is not null;

    public AppShellViewModel(
        IServiceProvider serviceProvider,
        IAuthStore authStore,
        IDbPathProvider dbPathProvider,
        ToastManager toastManager,
        DialogManager dialogManager,
        IMessenger messenger,
        IUpdateService updateService
    ) : base(messenger)
    {
        _serviceProvider = serviceProvider;
        _authStore = authStore;
        _dbPathProvider = dbPathProvider;
        _toastManager = toastManager;
        _dialogManager = dialogManager;
        _updateService = updateService;
        _currentViewModel = ActivatorUtilities.CreateInstance<LoadingViewModel>(_serviceProvider);

        Messenger.RegisterAll(this);
    }

    public bool IsLoggedIn => State == AppShellState.LoggedIn;
    public bool IsLoggedOut => State == AppShellState.LoggedOut;

    public async Task InitializeAsync()
    {
        _ = CheckForUpdateAsync();

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
            CurrentViewModel = ActivatorUtilities.CreateInstance<AuthViewModel>(_serviceProvider);
            State = AppShellState.LoggedOut;
        }
    }

    private async Task CheckForUpdateAsync()
    {
        var update = await _updateService.CheckForUpdateAsync();
        if (update is null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            AvailableUpdate = update;
            OnPropertyChanged(nameof(IsUpdateAvailable));
        });
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
