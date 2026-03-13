using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services;
using Fennec.App.Themes;

namespace Fennec.App.ViewModels.Settings;

public partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;

    public IReadOnlyList<ThemePalette> AvailablePalettes => AppThemes.AllPalettes;

    [ObservableProperty]
    private ThemePalette _selectedPalette;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightSelected))]
    [NotifyPropertyChangedFor(nameof(IsAutoSelected))]
    [NotifyPropertyChangedFor(nameof(IsDarkSelected))]
    private ThemeMode _selectedMode;

    public bool IsLightSelected => SelectedMode == AppThemes.Light;
    public bool IsAutoSelected => SelectedMode == AppThemes.Auto;
    public bool IsDarkSelected => SelectedMode == AppThemes.Dark;

    public AppearanceSettingsViewModel(ISettingsStore settingsStore, AppSettings settings)
    {
        _settingsStore = settingsStore;
        _selectedPalette = AppThemes.PaletteFromName(settings.Theme);
        _selectedMode = AppThemes.ModeFromName(settings.ThemeMode);
    }

    [RelayCommand]
    private void SelectLight() => SelectedMode = AppThemes.Light;

    [RelayCommand]
    private void SelectAuto() => SelectedMode = AppThemes.Auto;

    [RelayCommand]
    private void SelectDark() => SelectedMode = AppThemes.Dark;

    partial void OnSelectedPaletteChanged(ThemePalette value) => ApplyTheme();
    partial void OnSelectedModeChanged(ThemeMode value) => ApplyTheme();

    private void ApplyTheme()
    {
        var app = Application.Current!;
        var osTheme = GetOsTheme();
        app.RequestedThemeVariant = AppThemes.Resolve(SelectedPalette, SelectedMode, osTheme);
        _ = SaveThemeAsync();
    }

    private async Task SaveThemeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        settings.Theme = SelectedPalette.Name;
        settings.ThemeMode = SelectedMode.Name;
        await _settingsStore.SaveAsync(settings);
    }

    private static ThemeVariant? GetOsTheme()
    {
        if (Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            var ptv = desktop.MainWindow.PlatformSettings?.GetColorValues().ThemeVariant;
            if (ptv is null) return null;
            return ptv == Avalonia.Platform.PlatformThemeVariant.Light
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }
        return null;
    }
}
