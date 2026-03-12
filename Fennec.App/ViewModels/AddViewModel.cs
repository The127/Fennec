using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Routes;
using Fennec.App.Routing;
using Fennec.Client;

namespace Fennec.App.ViewModels;

public partial class AddViewModel(IRouter router, IFennecClient client, IMessenger messenger) : ObservableObject
{
    [RelayCommand]
    private async Task NavigateToCreateServerAsync()
    {
        await router.NavigateAsync(new CreateServerRoute(client, router, messenger));
    }

    [RelayCommand]
    private async Task NavigateToJoinServerAsync()
    {
        await router.NavigateAsync(new JoinServerRoute(client, router, messenger));
    }
}
