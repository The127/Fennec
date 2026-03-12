using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;

namespace Fennec.App.Routes;

public record CreateServerRoute(IFennecClient Client, IRouter Router, IMessenger Messenger) : IRoute
{
    public ObservableObject GetViewModel()
    {
        return new CreateServerViewModel(Client, Router, Messenger);
    }
}
