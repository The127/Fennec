using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.ViewModels;

public partial class AuthViewModel : ObservableRecipient, IRecipient<AuthNavigationMessage>
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private ObservableObject _currentViewModel;

    public AuthViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _currentViewModel = ActivatorUtilities.CreateInstance<LoginViewModel>(serviceProvider);
        Messenger.RegisterAll(this);
    }

    public void Receive(AuthNavigationMessage message)
    {
        CurrentViewModel = message.Target switch
        {
            AuthNavigationTarget.Login => ActivatorUtilities.CreateInstance<LoginViewModel>(_serviceProvider),
            AuthNavigationTarget.Register => ActivatorUtilities.CreateInstance<RegisterViewModel>(_serviceProvider),
            _ => throw new InvalidOperationException("Unknown target: " + message.Target)
        };
    }
}