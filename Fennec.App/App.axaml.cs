using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Fennec.App.Routing;
using Fennec.App.Services.Auth;
using Fennec.App.ViewModels;
using Fennec.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App;

public partial class App : Application
{
    public const string AppName = "FennecApp";
    
    private IServiceProvider _services = null!;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void ConfigureServices(Action<ServiceCollection>? configureAdditionalServices = null)
    {
        var services = new ServiceCollection();
        
        ConfigureDefaultServices(services);
        configureAdditionalServices?.Invoke(services);
        
        _services = services.BuildServiceProvider();
    }

    private void ConfigureDefaultServices(ServiceCollection services)
    {
        services.AddSingleton<IRouter, Router>();
        services.AddSingleton<IRouteStore>(sp => new MemoryRouteStore(10, 100));
        
        services.AddSingleton<IAuthService, AuthService>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var mainViewModel = ActivatorUtilities.CreateInstance<MainViewModel>(_services);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}