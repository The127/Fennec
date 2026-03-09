using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.Routing;

public class Router(IServiceProvider serviceProvider) : IRouter
{
    private readonly IRouteStore _routeStore = serviceProvider.GetRequiredService<IRouteStore>();

    public event EventHandler<ObservableObject>? Navigated;


    public async Task NavigateAsync(IRoute route, CancellationToken cancellationToken = default)
    {
        var viewModel = await _routeStore.PushAsync(route, cancellationToken);
        Navigated?.Invoke(this, viewModel);
    }

    public async Task NavigateBackAsync(CancellationToken cancellationToken = default)
    {
        var viewModel = await _routeStore.GoBackAsync(cancellationToken);
        if (viewModel is null) return;
        
        Navigated?.Invoke(this, viewModel);
    }

    public async Task NavigateForwardAsync(CancellationToken cancellationToken = default)
    {
        var viewModel = await _routeStore.GoForwardAsync(cancellationToken);
        if (viewModel is null) return;
        
        Navigated?.Invoke(this, viewModel);       
    }
}