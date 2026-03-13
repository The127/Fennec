using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;
using ShadUI;

using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.Routes;

public record CreateInviteRoute(
    IFennecClient Client,
    IRouter Router,
    ToastManager ToastManager,
    Guid ServerId,
    string InstanceUrl
) : IRoute
{
    public ObservableObject GetViewModel(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<CreateInviteViewModel>(serviceProvider, Client, Router, ToastManager, ServerId, InstanceUrl);
    }
}
