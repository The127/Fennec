using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _username = "";
}