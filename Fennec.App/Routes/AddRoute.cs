using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;

namespace Fennec.App.Routes;

public record AddRoute(IRouter Router, IFennecClient Client, IMessenger Messenger) : IRoute
{
    public ObservableObject GetViewModel()
    {
        return new AddViewModel(Router, Client, Messenger);
    }
}
