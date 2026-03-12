using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;
using ShadUI;

namespace Fennec.App.Routes;

public record CreateInviteRoute(
    IFennecClient Client,
    IRouter Router,
    ToastManager ToastManager,
    Guid ServerId,
    string InstanceUrl
) : IRoute
{
    public ObservableObject GetViewModel()
    {
        return new CreateInviteViewModel(Client, Router, ToastManager, ServerId, InstanceUrl);
    }
}
