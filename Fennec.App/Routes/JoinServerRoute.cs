using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;

using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.Routes;

public record JoinServerRoute(IFennecClient Client, IRouter Router, IMessenger Messenger, string InstanceUrl) : IRoute
{
    public ObservableObject GetViewModel(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<JoinServerViewModel>(serviceProvider, Client, Router, Messenger, InstanceUrl);
    }
}
