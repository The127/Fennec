using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.Routing;

public interface IRouteStore
{
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    Task<ObservableObject> PushAsync(IRoute route, IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
    Task<ObservableObject?> GoBackAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
    Task<ObservableObject?> GoForwardAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}