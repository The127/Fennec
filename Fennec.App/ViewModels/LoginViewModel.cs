using System.ComponentModel.DataAnnotations;
using Avalonia.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using Fennec.App.Validators;
using ShadUI;

namespace Fennec.App.ViewModels;

[ObservableRecipient]
public partial class LoginViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Username is required")]
    [UsernameFormat]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = "";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Password is required")]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = "";

    private readonly IAuthService _authService;
    private readonly ToastManager _toastManager;

    public LoginViewModel(IAuthService authService, IMessenger messenger, ToastManager toastManager)
    {
        _authService = authService;
        _toastManager = toastManager;
        Messenger = messenger;
    }

    private bool CanLogin() => !HasErrors && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task Login(CancellationToken cancellationToken)
    {
        ValidateAllProperties();

        if (HasErrors)
            return;
        
        var usernameParts = Username.Split('@');

        if (usernameParts.Length != 2)
            return;

        var username = usernameParts[0];
        var instanceUrl = usernameParts[1];

        try
        {
            var authSession = await _authService.LoginAsync(
                username,
                Password,
                instanceUrl,
                cancellationToken);
            
            Messenger.Send(new LoginSucceededMessage(authSession!)); // TODO: deal with the nulablility
        }
        catch (Exception)
        {
            _toastManager.CreateToast("Failed to login")
                .WithContent("An error occurred while logging in.")
                .ShowError();
        }

    }

    [RelayCommand]
    private void NavigateToRegister()
    {
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Register));
    }
}