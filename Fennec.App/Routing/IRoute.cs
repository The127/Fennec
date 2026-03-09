using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.Routing;

public interface IRoute
{
    ObservableObject GetViewModel();
}