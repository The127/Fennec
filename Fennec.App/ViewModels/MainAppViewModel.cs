using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.Services.Auth;

namespace Fennec.App.ViewModels;

public partial class MainAppViewModel(IRouter router, IAuthStore authStore, IMessenger messenger) : ObservableObject
{
    [ObservableProperty]
    private IRouter _router = router;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _userAtServer = string.Empty;

    [ObservableProperty]
    private string _avatarFallback = string.Empty;

    public async Task InitializeAsync()
    {
        var session = await authStore.GetCurrentAuthSessionAsync();
        if (session is null) return;

        ApplySession(session);
    }

    public void ApplySession(AuthSession session)
    {
        Username = session.Username;
        UserAtServer = $"{session.Username}@{session.Url}";
        AvatarFallback = session.Username[..1].ToUpperInvariant();
    }

    [RelayCommand]
    private async Task Logout()
    {
        var session = await authStore.GetCurrentAuthSessionAsync();
        if (session is not null)
        {
            await authStore.RemoveSessionAsync(session);
        }

        messenger.Send(new UserLoggedOutMessage());
    }
}
