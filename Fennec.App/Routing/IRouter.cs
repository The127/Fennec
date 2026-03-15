using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.Routing;

public interface IRouter
{
    ObservableObject? CurrentViewModel { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    event EventHandler<ObservableObject>? Navigated;
    Task NavigateAsync(IRoute route, CancellationToken cancellationToken = default);
    Task NavigateBackAsync(CancellationToken cancellationToken = default);
    Task NavigateForwardAsync(CancellationToken cancellationToken = default);
}