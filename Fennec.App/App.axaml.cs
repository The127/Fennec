using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Exceptions;
using Fennec.App.Logger;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Shortcuts;
using Fennec.Client;
using Fennec.App.Services.Storage;
using Fennec.App.Themes;
using Fennec.App.ViewModels;
using Fennec.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadUI;

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
        
        // Ensure database is created if we have a path
        using var scope = _services.CreateScope();
        var dbPathProvider = scope.ServiceProvider.GetRequiredService<IDbPathProvider>();
        if (dbPathProvider.CurrentDbPath == null)
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // db.Database.EnsureCreated();
        
        // We use a simple way to ensure the latest schema is applied.
        // For a production app, we should use Migrations, but here we can use EnsureDeleted/EnsureCreated 
        // if we want to reset or a more sophisticated approach.
        // However, the error "no such table" usually means the DB existed but was old.
        // Since we don't have migrations yet, we'll use a hack to add missing tables or just recreate if development.
        try 
        {
            db.Database.EnsureCreated();
            // Try a simple query to see if the new table exists
            _ = db.ChannelGroups.Count();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Table missing? In this stage of development, we might just recreate the DB.
            // OR we can try to create the missing tables.
            // For now, let's just log and maybe suggest a manual delete of the db file if it's a dev machine.
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
            logger.LogWarning(ex, "Database schema mismatch detected. Recreating database.");
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        }
    }

    private void ConfigureDefaultServices(ServiceCollection services)
    {
        services.AddLogging(builder =>
        {
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#else
        builder.SetMinimumLevel(LogLevel.Information);
#endif
            builder.AddProvider(new ConsoleLoggerProvider());
        });

        services.AddSingleton<ToastManager>();
        services.AddSingleton<IExceptionHandler, ExceptionHandler>();
        services.AddSingleton(new DialogManager()
            .Register<Views.CreateChannelDialogView, ViewModels.CreateChannelDialogViewModel>()
            .Register<Views.QuickNavDialogView, ViewModels.QuickNavDialogViewModel>()
            .Register<Views.Settings.SettingsView, ViewModels.Settings.SettingsViewModel>()
            .Register<Views.SwitchAccountView, ViewModels.SwitchAccountViewModel>());
        services.AddSingleton<IRouter, Router>();
        services.AddSingleton<IClientFactory, ClientFactory>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ServerRepository>();
        services.AddSingleton<IServerRepository>(sp => sp.GetRequiredService<ServerRepository>());
        services.AddSingleton<IChannelGroupRepository>(sp => sp.GetRequiredService<ServerRepository>());
        services.AddSingleton<IChannelRepository>(sp => sp.GetRequiredService<ServerRepository>());
        services.AddSingleton<IServerStore, ServerStore>();
        services.AddSingleton<IKeymapService, KeymapService>();
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
        services.AddSingleton<IMessageHubClient, MessageHubClient>();
        services.AddSingleton<IMessageHubService, MessageHubService>();
        services.AddSingleton<IVoiceHubService, VoiceHubService>();
        services.AddSingleton<IVoiceCallService, VoiceCallService>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ApplySavedTheme();

        var mainViewModel = ActivatorUtilities.CreateInstance<AppShellViewModel>(_services);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
            mainWindow.AttachShortcutDispatcher(_services.GetRequiredService<IKeymapService>());
            desktop.MainWindow = mainWindow;

            SubscribeToOsThemeChanges(mainWindow);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new AppShellView
            {
                DataContext = mainViewModel,
            };
        }

        Task.Run(mainViewModel.InitializeAsync);

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplySavedTheme()
    {
        var settingsStore = _services.GetService<ISettingsStore>();
        if (settingsStore is null) return;

        var settings = Task.Run(() => settingsStore.LoadAsync()).GetAwaiter().GetResult();
        var mode = AppThemes.ModeFromName(settings.ThemeMode);
        // For Auto mode at startup, we can't read PlatformSettings yet (no window),
        // so fall back to Dark. The subscription will correct it once the window is ready.
        RequestedThemeVariant = AppThemes.Resolve(settings.Theme,
            AppThemes.ResolveEffectiveMode(mode));
    }

    private void SubscribeToOsThemeChanges(MainWindow window)
    {
        var platformSettings = window.PlatformSettings;
        if (platformSettings is null) return;

        // Apply correct theme now that we have PlatformSettings (fixes Auto at startup)
        ReapplyIfAutoMode(ToThemeVariant(platformSettings.GetColorValues().ThemeVariant));

        platformSettings.ColorValuesChanged += (_, values) =>
        {
            ReapplyIfAutoMode(ToThemeVariant(values.ThemeVariant));
        };
    }

    private static ThemeVariant ToThemeVariant(Avalonia.Platform.PlatformThemeVariant ptv) =>
        ptv == Avalonia.Platform.PlatformThemeVariant.Light ? ThemeVariant.Light : ThemeVariant.Dark;

    private void ReapplyIfAutoMode(ThemeVariant osTheme)
    {
        var settingsStore = _services.GetService<ISettingsStore>();
        if (settingsStore is null) return;

        var settings = Task.Run(() => settingsStore.LoadAsync()).GetAwaiter().GetResult();
        var mode = AppThemes.ModeFromName(settings.ThemeMode);
        if (mode != AppThemes.Auto) return;

        RequestedThemeVariant = AppThemes.Resolve(
            settings.Theme,
            AppThemes.ResolveEffectiveMode(mode, osTheme));
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