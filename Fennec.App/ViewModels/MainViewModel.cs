using System.Linq.Expressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Routes;
using Fennec.App.Routing;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authStore;
    private readonly IRouter _router;
    
    public IRouter Router => _router;

    public MainViewModel(IServiceProvider serviceProvider, 
        IAuthService authStore,
        IRouter router)
    {
        _serviceProvider = serviceProvider;
        _authStore = authStore;
        _router = router;
    }
}