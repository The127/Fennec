using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;

namespace Fennec.App.ViewModels;

public partial class MainAppViewModel(IRouter router) : ObservableObject
{
    [ObservableProperty]
    private IRouter _router = router;
}