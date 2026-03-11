using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Validators;
using ShadUI;

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
    private readonly ToastManager _toastManager;

    public RegisterViewModel(
        IAuthService authService,
        IMessenger messenger,
        ToastManager toastManager
    )
    {
        _authService = authService;
        _toastManager = toastManager;
        Messenger = messenger;
    }

    private bool CanRegister() => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    
    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task Register(CancellationToken cancellationToken)
    {
        var usernameParts = Username.Split('@');
        var username = usernameParts[0];
        var instanceUrl = usernameParts[1];

        try
        {
            await _authService.RegisterAsync(username, Password, instanceUrl, cancellationToken);
            Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
        }
        catch (Exception)
        {
            _toastManager.CreateToast("Failed to register")
                .WithContent("An error occurred while registering.")
                .ShowError();
        }
    }
    
    [RelayCommand]
    private void NavigateToLogin()
    {
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
    }
}