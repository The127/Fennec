using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Fennec.App;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .AfterSetup(_ =>
        {
            if (Application.Current is App app)
            {
                app.ConfigureServices();
            }
        })
        .WithInterFont()
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}