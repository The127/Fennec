using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.Routing;

public interface IRouteStore
{
    Task<ObservableObject> PushAsync(IRoute route, CancellationToken cancellationToken = default);
    Task<ObservableObject?> GoBackAsync(CancellationToken cancellationToken = default);
    Task<ObservableObject?> GoForwardAsync(CancellationToken cancellationToken = default);
}