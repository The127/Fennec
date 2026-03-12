using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;
using ShadUI;

using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.Routes;

public record ServerRoute(IFennecClient Client, DialogManager DialogManager, Guid ServerId, string ServerName, string InstanceUrl) : IRoute
{
    public ObservableObject GetViewModel(IServiceProvider serviceProvider)
    {
        var vm = new ServerViewModel(Client, DialogManager, ServerId, ServerName, InstanceUrl);
        _ = vm.LoadAsync();
        return vm;
    }
}
