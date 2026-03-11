using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel
    : ObservableRecipient, IRecipient<LoginSucceededMessage>
{
    [ObservableProperty]
    private ObservableObject _currentViewModel;

    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthStore _authStore;

    public AppShellViewModel(IServiceProvider serviceProvider, IAuthStore authStore)
    {
        _serviceProvider = serviceProvider;
        _authStore = authStore;
        _currentViewModel = ActivatorUtilities.CreateInstance<LoadingViewModel>(_serviceProvider);

        Messenger.RegisterAll(this);
    }

    public async Task InitializeAsync()
    {
        var currentSession = await _authStore.GetCurrentAuthSessionAsync();
        CurrentViewModel = currentSession switch
        {
            not null => ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider),
            _ => ActivatorUtilities.CreateInstance<AuthViewModel>(_serviceProvider),
        };
    }

    public void Receive(LoginSucceededMessage message)
    {
        CurrentViewModel = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider);
    }
}