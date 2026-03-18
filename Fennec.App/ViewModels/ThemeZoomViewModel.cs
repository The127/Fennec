using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.Themes;
using ShadUI;
using ThemeMode = Fennec.App.Themes.ThemeMode;

namespace Fennec.App.ViewModels;

public partial class ThemeZoomViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IMessenger _messenger;
    private readonly ToastManager _toastManager;

    private const double ZoomStep = 0.1;
    private const double ZoomMin = 0.5;
    private const double ZoomMax = 2.0;

    public ThemeZoomViewModel(ISettingsStore settingsStore, IMessenger messenger, ToastManager toastManager)
    {
        _settingsStore = settingsStore;
        _messenger = messenger;
        _toastManager = toastManager;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightMode))]
    [NotifyPropertyChangedFor(nameof(IsAutoMode))]
    [NotifyPropertyChangedFor(nameof(IsDarkMode))]
    private ThemeMode _currentThemeMode = AppThemes.Auto;

    public bool IsLightMode => CurrentThemeMode == AppThemes.Light;
    public bool IsAutoMode => CurrentThemeMode == AppThemes.Auto;
    public bool IsDarkMode => CurrentThemeMode == AppThemes.Dark;

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        CurrentThemeMode = AppThemes.ModeFromName(settings.ThemeMode);
        _messenger.Send(new ZoomChangedMessage(settings.ZoomLevel));
    }

    [RelayCommand]
    private async Task ZoomInAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        var newZoom = Math.Min(settings.ZoomLevel + ZoomStep, ZoomMax);
        await ApplyZoomAsync(settings, newZoom);
    }

    [RelayCommand]
    private async Task ZoomOutAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        var newZoom = Math.Max(settings.ZoomLevel - ZoomStep, ZoomMin);
        await ApplyZoomAsync(settings, newZoom);
    }

    [RelayCommand]
    private async Task ZoomResetAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        await ApplyZoomAsync(settings, 1.0);
    }

    private async Task ApplyZoomAsync(AppSettings settings, double zoomLevel)
    {
        zoomLevel = Math.Round(zoomLevel, 1);
        settings.ZoomLevel = zoomLevel;
        await _settingsStore.SaveAsync(settings);
        _messenger.Send(new ZoomChangedMessage(zoomLevel));
        _toastManager.CreateToast($"Zoom: {zoomLevel:P0}")
            .WithDelay(2)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task SetThemeModeAsync(string modeName)
    {
        var mode = AppThemes.ModeFromName(modeName);
        var app = Application.Current!;
        var settings = await _settingsStore.LoadAsync();
        var palette = AppThemes.PaletteFromName(settings.Theme);

        ThemeVariant? osTheme = null;
        if (app.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            var ptv = desktop.MainWindow.PlatformSettings?.GetColorValues().ThemeVariant;
            if (ptv is not null)
                osTheme = ptv == Avalonia.Platform.PlatformThemeVariant.Light
                    ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        app.RequestedThemeVariant = AppThemes.Resolve(palette, mode, osTheme);
        CurrentThemeMode = mode;
        settings.Theme = palette.Name;
        settings.ThemeMode = mode.Name;
        await _settingsStore.SaveAsync(settings);
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        var next = CurrentThemeMode == AppThemes.Light ? AppThemes.Auto
                 : CurrentThemeMode == AppThemes.Auto ? AppThemes.Dark
                 : AppThemes.Light;
        await SetThemeModeAsync(next.Name);
        _toastManager.CreateToast($"Mode: {next.Name}")
            .WithDelay(2)
            .ShowInfo();
    }
}
