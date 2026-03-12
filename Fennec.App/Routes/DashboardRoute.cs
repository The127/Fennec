using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;

namespace Fennec.App.Routes;

public record DashboardRoute : IRoute
{
    public ObservableObject GetViewModel()
    {
        return new DashboardViewModel();
    }
}
