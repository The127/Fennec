using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel : ObservableRecipient, IRecipient<LoginSucceededMessage>
{
    [ObservableProperty]
    private ObservableObject _currentViewModel;

    private readonly IServiceProvider _serviceProvider;

    public AppShellViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _currentViewModel = new LoginViewModel(serviceProvider.GetRequiredService<IAuthService>());
        
        Messenger.RegisterAll(this);
    }


    public void Receive(LoginSucceededMessage message)
    {
        CurrentViewModel = ActivatorUtilities.CreateInstance<MainAppViewModel>(_serviceProvider);
    }
}