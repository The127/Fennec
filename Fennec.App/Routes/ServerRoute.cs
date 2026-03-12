using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;

namespace Fennec.App.Routes;

public record ServerRoute(IFennecClient Client, Guid ServerId, string ServerName) : IRoute
{
    public ObservableObject GetViewModel()
    {
        var vm = new ServerViewModel(Client, ServerId, ServerName);
        _ = vm.LoadAsync();
        return vm;
    }
}
