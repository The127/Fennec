using Avalonia;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Desktop.Services;
using Fennec.App.Desktop.Services.Auth;
using Fennec.App.Desktop.Services.Storage;
using Fennec.App.Routing;
using Fennec.App.Services.Storage;
using Microsoft.EntityFrameworkCore;
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
            .ConfigureFonts(fontManager =>
            {
                fontManager.AddFontCollection(new Avalonia.Media.Fonts.EmbeddedFontCollection(
                    new Uri("fonts:NotoColorEmoji", UriKind.Absolute),
                    new Uri("avares://Fennec.App/Assets/Fonts", UriKind.Absolute)));
            })
            .WithDeveloperTools()
            .LogToTrace();
    
    private static void ConfigureAdditionalServices(ServiceCollection services)
    {
        services.AddSingleton<IRouteStore>(sp => new MemoryRouteStore(10, 100));
        services.AddSingleton<IAuthStore, DesktopAuthStore>();
        services.AddSingleton<ISettingsStore, DesktopSettingsStore>();
        services.AddSingleton<IDbPathProvider, DesktopDbPathProvider>();
        
        services.AddDbContextFactory<AppDbContext>();
    }
}