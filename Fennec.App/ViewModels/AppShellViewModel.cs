using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using Fennec.App.Services.Storage;
using Microsoft.EntityFrameworkCore;
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

    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthStore _authStore;
    private readonly IDbPathProvider _dbPathProvider;

    public AppShellViewModel(
        IServiceProvider serviceProvider,
        IAuthStore authStore,
        IDbPathProvider dbPathProvider,
        ToastManager toastManager,
        DialogManager dialogManager,
        IMessenger messenger
    ) : base(messenger)
    {
        _serviceProvider = serviceProvider;
        _authStore = authStore;
        _dbPathProvider = dbPathProvider;
        _toastManager = toastManager;
        _dialogManager = dialogManager;
        _currentViewModel = ActivatorUtilities.CreateInstance<LoadingViewModel>(_serviceProvider);

        Messenger.RegisterAll(this);
    }

    public bool IsLoggedIn => State == AppShellState.LoggedIn;
    public bool IsLoggedOut => State == AppShellState.LoggedOut;

    public async Task InitializeAsync()
    {
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
