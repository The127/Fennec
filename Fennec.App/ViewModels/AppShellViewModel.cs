using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel(
    IServiceProvider serviceProvider
) : ViewModelBase
{
    [ObservableProperty]
    private ObservableObject _currentViewModel = new LoginViewModel(serviceProvider.GetRequiredService<IAuthService>());
}