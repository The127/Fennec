using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using Fennec.Client;
using ShadUI;

namespace Fennec.App.Routes;

public record ServerRoute(IFennecClient Client, DialogManager DialogManager, IServerStore ServerStore, Guid ServerId, string ServerName, string InstanceUrl) : IRoute
{
    public ObservableObject GetViewModel(IServiceProvider serviceProvider)
    {
        var vm = new ServerViewModel(Client, DialogManager, ServerStore, ServerId, ServerName, InstanceUrl);
        _ = vm.LoadAsync();
        return vm;
    }
}
