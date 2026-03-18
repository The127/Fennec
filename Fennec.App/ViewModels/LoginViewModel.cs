using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain;
using Fennec.App.Exceptions;
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
    private readonly IExceptionHandler _exceptionHandler;
    private readonly DialogManager _dialogManager;
    private readonly IAuthStore _authStore;

    public LoginViewModel(
        IAuthService authService,
        IMessenger messenger,
        ToastManager toastManager,
        IExceptionHandler exceptionHandler,
        DialogManager dialogManager,
        IAuthStore authStore)
    {
        _authService = authService;
        _toastManager = toastManager;
        _exceptionHandler = exceptionHandler;
        _dialogManager = dialogManager;
        _authStore = authStore;
        Messenger = messenger;
    }

    public void NotifySchemeStripped()
    {
        _toastManager.CreateToast("URL scheme removed")
            .WithContent("https:// is used automatically.")
            .WithDelay(3)
            .ShowInfo();
    }

    private bool CanLogin() => !HasErrors && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task Login(CancellationToken cancellationToken)
    {
        ValidateAllProperties();

        if (HasErrors)
            return;
        
        var address = FederatedAddress.Parse(Username);
        var username = address.Username;
        var instanceUrl = address.InstanceUrl!;

        try
        {
            var authSession = await _authService.LoginAsync(
                username,
                Password,
                instanceUrl,
                cancellationToken);

            Messenger.Send(new LoginSucceededMessage(authSession!)); // TODO: deal with the nulablility
        }
        catch (Exception e)
        {
            _exceptionHandler.Handle(e, "Failed to login for user {User} on {Url}", Username, instanceUrl);
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

    [RelayCommand]
    private void NavigateToSwitchAccount()
    {
        var vm = new SwitchAccountViewModel(_dialogManager, _authStore);
        _dialogManager.CreateDialog(vm)
            .Dismissible()
            .WithSuccessCallback<SwitchAccountViewModel>(async ctx =>
            {
                if (ctx.SelectedSession is not null)
                {
                    await _authService.SwitchAccountAsync(ctx.SelectedSession);
                    Messenger.Send(new LoginSucceededMessage(ctx.SelectedSession));
                }
                // LoginRequested is a no-op here — we're already on the login page
            })
            .Show();
    }
}