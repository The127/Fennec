using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;

namespace Fennec.App.ViewModels;

public partial class MainAppViewModel : ObservableObject
{
    [ObservableProperty]
    private IRouter _router;

    public MainAppViewModel(IRouter router)
    {
        Router = router;
    }
}