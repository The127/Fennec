using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Validators;

namespace Fennec.App.ViewModels;

[ObservableRecipient]
public partial class RegisterViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Username is required")]
    [UsernameFormat]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _username = "";
    
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Password is required")]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _password = "";

    private readonly IAuthService _authService;

    public RegisterViewModel(IAuthService authService, IMessenger messenger)
    {
        _authService = authService;
        Messenger = messenger;
    }

    private bool CanRegister() => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    
    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task Register(CancellationToken cancellationToken)
    {
        var usernameParts = Username.Split('@');
        var username = usernameParts[0];
        var instanceUrl = usernameParts[1];
        await _authService.RegisterAsync(username, Password, instanceUrl, cancellationToken);
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
    }
    
    [RelayCommand]
    private void NavigateToLogin()
    {
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
    }
}