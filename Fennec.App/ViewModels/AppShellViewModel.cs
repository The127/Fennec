using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Services.Auth;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel(
    IServiceProvider serviceProvider
) : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentViewModel = new LoadingViewModel();
}