using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;

namespace Fennec.App.Routes;

public record ServerRoute(Guid ServerId, string ServerName) : IRoute
{
    public ObservableObject GetViewModel()
    {
        return new ServerViewModel
        {
            ServerId = ServerId,
            ServerName = ServerName,
        };
    }
}
