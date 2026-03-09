using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.Routing;

public interface IRouter
{
    event EventHandler<ObservableObject>? Navigated;
    Task NavigateAsync(IRoute route, CancellationToken cancellationToken = default);
    Task NavigateBackAsync(CancellationToken cancellationToken = default);
    Task NavigateForwardAsync(CancellationToken cancellationToken = default);   
}