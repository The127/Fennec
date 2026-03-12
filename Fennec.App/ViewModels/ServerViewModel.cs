using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.ViewModels;

public partial class ServerViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _serverId;

    [ObservableProperty]
    private string _serverName = string.Empty;
}
