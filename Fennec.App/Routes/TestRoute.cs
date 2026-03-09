using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;

namespace Fennec.App.Routes;

public record TestRoute(int Counter) : IRoute
{
    public ObservableObject GetViewModel()
    {
        return new TestViewModel
        {
            Counter = Counter,
        };
    }
}