using Avalonia;
using Avalonia.Browser;
using Fennec.App;
using Fennec.App.Routing;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class Program
{
    private static Task Main(string[] _) => BuildAvaloniaApp()
        .WithInterFont()
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() =>
        {
            var app = new App();
            app.ConfigureServices(ConfigureAdditionalServices);
            return app;
        });
    
    private static void ConfigureAdditionalServices(ServiceCollection services)
    {
        services.AddSingleton<IRouteStore>(sp => new MemoryRouteStore(10, 100));
        // TODO: implement and register auth storage for browser
    }
}