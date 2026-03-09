using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.ViewModels;

public partial class AppShellViewModel(
    IServiceProvider serviceProvider
) : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentViewModel = new LoginViewModel();
}