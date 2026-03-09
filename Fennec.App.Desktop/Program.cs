using Avalonia;
using Fennec.App.Services.Auth;
using Fennec.App.Desktop.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() =>
            {
                var app = new App();
                app.ConfigureServices(ConfigureAdditionalServices);
                return app;
            })
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    
    private static void ConfigureAdditionalServices(ServiceCollection services)
    {
        services.AddSingleton<IAuthStore, DesktopAuthStore>();
    }
}