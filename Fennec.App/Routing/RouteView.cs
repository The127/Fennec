using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.Routing;

public class RouteView : ContentControl
{
    public static readonly StyledProperty<IRouter?> RouterProperty =
        AvaloniaProperty.Register<RouteView, IRouter?>(nameof(Router));

    private IRouter? _router;

    static RouteView()
    {
        RouterProperty.Changed.AddClassHandler<RouteView>((view, _) =>
        {
            view.AttachRouter();
        });
    }

    public IRouter? Router
    {
        get => GetValue(RouterProperty);
        set => SetValue(RouterProperty, value);
    }

    private void AttachRouter()
    {
        if (_router != null)
        {
            _router.Navigated -= OnNavigated;
        }

        _router = Router;

        if (_router != null)
        {
            _router.Navigated += OnNavigated;
        }
    }

    private void OnNavigated(object? sender, ObservableObject vm)
    {
        Content = vm;
    }
}