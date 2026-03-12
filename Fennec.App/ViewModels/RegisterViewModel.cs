using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Exceptions;
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
    private string? _displayName = "";
    
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Password is required")]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _password = "";

    private readonly IAuthService _authService;
    private readonly ToastManager _toastManager;
    private readonly IExceptionHandler _exceptionHandler;

    public RegisterViewModel(
        IAuthService authService,
        IMessenger messenger,
        ToastManager toastManager,
        IExceptionHandler exceptionHandler
    )
    {
        _authService = authService;
        _toastManager = toastManager;
        _exceptionHandler = exceptionHandler;
        Messenger = messenger;
    }

    public void NotifySchemeStripped()
    {
        _toastManager.CreateToast("URL scheme removed")
            .WithContent("https:// is used automatically.")
            .WithDelay(3)
            .ShowInfo();
    }

    private bool CanRegister() => !HasErrors && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    
    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task Register(CancellationToken cancellationToken)
    {
        ValidateAllProperties();

        if (HasErrors)
            return;
        
        var usernameParts = Username.Split('@');
        var username = usernameParts[0];
        var instanceUrl = usernameParts[1];

        try
        {
            await _authService.RegisterAsync(username, DisplayName, Password, instanceUrl, cancellationToken);
            Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
        }
        catch (Exception e)
        {
            _exceptionHandler.Handle(e, "Failed to register {User} on {Url}", Username, instanceUrl);
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