using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.ViewModels;

namespace Fennec.App.Routing;

public interface IRouter
{
    event EventHandler<ObservableObject>? Navigated;
    Task NavigateAsync(IRoute route, CancellationToken cancellationToken = default);
    Task NavigateBackAsync(CancellationToken cancellationToken = default);
    Task NavigateForwardAsync(CancellationToken cancellationToken = default);   
}