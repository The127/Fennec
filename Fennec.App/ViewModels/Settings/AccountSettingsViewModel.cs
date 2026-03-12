using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.ViewModels.Settings;

public partial class AccountSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _serverUrl;

    public AccountSettingsViewModel(string username, string serverUrl)
    {
        _username = username;
        _serverUrl = serverUrl;
    }
}
