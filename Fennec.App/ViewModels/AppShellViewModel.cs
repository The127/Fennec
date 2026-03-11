using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.DependencyInjection;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel
    : ObservableRecipient, IRecipient<LoginSucceededMessage>
{
    [ObservableProperty]
    private ObservableObject _currentViewModel;
    
    [ObservableProperty]
    private ToastManager _toastManager;

    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthStore _authStore;

    public AppShellViewModel(
        IServiceProvider serviceProvider,
        IAuthStore authStore, 
        ToastManager toastManager
    )
    {
        _serviceProvider = serviceProvider;
        _authStore = authStore;
        _toastManager = toastManager;
        _currentViewModel = ActivatorUtilities.CreateInstance<LoadingViewModel>(_serviceProvider);

        Messenger.RegisterAll(this);
    }

    public async Task InitializeAsync()
    {
        var currentSession = await _authStore.GetCurrentAuthSessionAsync();
        if (currentSession is not null)
        {
            var vm = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider);
            await vm.InitializeAsync();
            CurrentViewModel = vm;
        }
        else
        {
            CurrentViewModel = ActivatorUtilities.CreateInstance<AuthViewModel>(_serviceProvider);
        }
    }

    public void Receive(LoginSucceededMessage message)
    {
        var vm = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider);
        vm.ApplySession(message.Session);
        CurrentViewModel = vm;
    }
}