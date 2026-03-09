using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services.Auth;

namespace Fennec.App.ViewModels;

public partial class LoginViewModel(IAuthService authService) : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = "";
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = "";

    private bool CanLogin() => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task Login(CancellationToken cancellationToken)
    {
        await authService.Login(Username, Password, "https://localhost:7014/", cancellationToken);
    }
}