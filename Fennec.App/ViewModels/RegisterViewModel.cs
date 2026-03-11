using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using CommunityToolkit.Mvvm.Messaging;

namespace Fennec.App.ViewModels;

public partial class RegisterViewModel(IAuthService authService) : ObservableRecipient
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _username = "";
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _password = "";

    private bool CanRegister() => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    
    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task Register(CancellationToken cancellationToken)
    {
        var usernameParts = Username.Split('@');
        var username = usernameParts[0];
        var instanceUrl = usernameParts[1];
        await authService.RegisterAsync(username, Password, instanceUrl, cancellationToken);
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
    }
    
    [RelayCommand]
    private void NavigateToLogin()
    {
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
    }
}