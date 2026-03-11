using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.DependencyInjection;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel
    : ObservableRecipient, IRecipient<LoginSucceededMessage>, IRecipient<UserLoggedOutMessage>
{
    [ObservableProperty]
    private ObservableObject _currentViewModel;

    [ObservableProperty]
    private AppShellState _state = AppShellState.Loading;

    [ObservableProperty]
    private ToastManager _toastManager;

    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthStore _authStore;

    public AppShellViewModel(
        IServiceProvider serviceProvider,
        IAuthStore authStore,
        ToastManager toastManager,
        IMessenger messenger
    ) : base(messenger)
    {
        _serviceProvider = serviceProvider;
        _authStore = authStore;
        _toastManager = toastManager;
        _currentViewModel = ActivatorUtilities.CreateInstance<LoadingViewModel>(_serviceProvider);

        Messenger.RegisterAll(this);
    }

    public bool IsLoggedIn => State == AppShellState.LoggedIn;
    public bool IsLoggedOut => State == AppShellState.LoggedOut;

    public async Task InitializeAsync()
    {
        var currentSession = await _authStore.GetCurrentAuthSessionAsync();
        if (currentSession is not null)
        {
            var vm = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider, Messenger);
            await vm.InitializeAsync();
            CurrentViewModel = vm;
            State = AppShellState.LoggedIn;
        }
        else
        {
            CurrentViewModel = ActivatorUtilities.CreateInstance<AuthViewModel>(_serviceProvider);
            State = AppShellState.LoggedOut;
        }
    }

    public void Receive(LoginSucceededMessage message)
    {
        var vm = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider, Messenger);
        vm.ApplySession(message.Session);
        CurrentViewModel = vm;
        State = AppShellState.LoggedIn;
    }

    public void Receive(UserLoggedOutMessage message)
    {
        CurrentViewModel = ActivatorUtilities.CreateInstance<AuthViewModel>(_serviceProvider);
        State = AppShellState.LoggedOut;
    }
}
