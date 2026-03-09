using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.ViewModels;

namespace Fennec.App.Routing;

public interface IRoute
{
    ObservableObject GetViewModel();
}