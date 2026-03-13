using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using System.Collections.ObjectModel;

namespace Fennec.App.ViewModels;

public partial class SwitchAccountViewModel : ObservableRecipient
{
    private readonly IAuthService _authService;
    private readonly IAuthStore _authStore;

    [ObservableProperty]
    private ObservableCollection<AuthSession> _sessions = [];

    [ObservableProperty]
    private bool _isLoading;

    public SwitchAccountViewModel(
        IAuthService authService,
        IAuthStore authStore
    )
    {
        _authService = authService;
        _authStore = authStore;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var sessions = await _authStore.GetSessionsAsync();
            Sessions = new ObservableCollection<AuthSession>(sessions);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SwitchAccount(AuthSession session)
    {
        await _authService.SwitchAccountAsync(session);
        Messenger.Send(new LoginSucceededMessage(session));
    }

    [RelayCommand]
    private void NavigateToLogin()
    {
        Messenger.Send(new AuthNavigationMessage(AuthNavigationTarget.Login));
    }
}
