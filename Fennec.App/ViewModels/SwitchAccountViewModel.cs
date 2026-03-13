using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services.Auth;
using ShadUI;
using System.Collections.ObjectModel;

namespace Fennec.App.ViewModels;

public partial class SwitchAccountViewModel : ObservableObject
{
    private readonly DialogManager _dialogManager;
    private readonly IAuthStore _authStore;
    private readonly Guid? _currentUserId;

    [ObservableProperty]
    private ObservableCollection<AuthSession> _sessions = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private AuthSession? _selectedSession;

    public bool LoginRequested { get; private set; }

    public SwitchAccountViewModel(
        DialogManager dialogManager,
        IAuthStore authStore,
        Guid? currentUserId = null)
    {
        _dialogManager = dialogManager;
        _authStore = authStore;
        _currentUserId = currentUserId;
        _ = LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        IsLoading = true;
        try
        {
            var sessions = await _authStore.GetSessionsAsync();
            if (_currentUserId is not null)
                sessions = sessions.Where(s => s.UserId != _currentUserId.Value).ToList();
            Sessions = new ObservableCollection<AuthSession>(sessions);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectAccount(AuthSession session)
    {
        SelectedSession = session;
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void LoginWithAnotherAccount()
    {
        LoginRequested = true;
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}
