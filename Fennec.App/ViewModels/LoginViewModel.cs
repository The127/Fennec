using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;

namespace Fennec.App.ViewModels;

public partial class LoginViewModel(IAuthService authService) : ObservableRecipient
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public string _username = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public string _password = "";

    private bool CanLogin() => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task Login(CancellationToken cancellationToken)
    {
        var usernameParts = Username.Split('@');
        var username = usernameParts[0];
        var instanceUrl = usernameParts[1];
        var authSession = await authService.Login(username, Password, instanceUrl, cancellationToken);
        Messenger.Send(new LoginSucceededMessage(authSession!)); // TODO: deal with the nulablility
    }

    [RelayCommand]
    private void NavigateToRegister()
    {
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Register));
    }
}