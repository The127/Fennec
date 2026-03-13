using Avalonia;
using Avalonia.Browser;
using Fennec.App;
using Fennec.App.Browser.Services.Auth;
using Fennec.App.Browser.Services.Storage;
using Fennec.App.Routing;
using Fennec.App.Services.Auth;
using Fennec.App.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class Program
{
    private static Task Main(string[] _) => BuildAvaloniaApp()
        .WithInterFont()
        .ConfigureFonts(fontManager =>
        {
            fontManager.AddFontCollection(new Avalonia.Media.Fonts.EmbeddedFontCollection(
                new Uri("fonts:NotoColorEmoji", UriKind.Absolute),
                new Uri("avares://Fennec.App/Assets/Fonts", UriKind.Absolute)));
        })
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
        services.AddSingleton<IRouteStore>(_ => new MemoryRouteStore(10, 100));
        services.AddSingleton<IAuthStore, BrowserAuthStore>();
        services.AddSingleton<IDbPathProvider, BrowserDbPathProvider>();

        // Browser SQLite typically uses an in-memory db or 
        // a specialized IDB/WASM persistence driver. 
        // For standard EF Core SQLite, we'll start with in-memory 
        // or standard Sqlite (which on Wasm can be backed by IDB)
        services.AddDbContext<AppDbContext>();
    }
}