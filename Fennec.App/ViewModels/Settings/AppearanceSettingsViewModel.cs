using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Services;
using Fennec.App.Themes;

namespace Fennec.App.ViewModels.Settings;

public partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;

    public IReadOnlyList<ThemePalette> AvailablePalettes => AppThemes.AllPalettes;
    public IReadOnlyList<ThemeMode> AvailableModes => AppThemes.AllModes;

    [ObservableProperty]
    private ThemePalette _selectedPalette;

    [ObservableProperty]
    private ThemeMode _selectedMode;

    public AppearanceSettingsViewModel(ISettingsStore settingsStore, AppSettings settings)
    {
        _settingsStore = settingsStore;
        _selectedPalette = AppThemes.PaletteFromName(settings.Theme);
        _selectedMode = AppThemes.ModeFromName(settings.ThemeMode);
    }

    partial void OnSelectedPaletteChanged(ThemePalette value) => ApplyTheme();
    partial void OnSelectedModeChanged(ThemeMode value) => ApplyTheme();

    private void ApplyTheme()
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = AppThemes.Resolve(SelectedPalette, SelectedMode);
        _ = _settingsStore.SaveAsync(new AppSettings
        {
            Theme = SelectedPalette.Name,
            ThemeMode = SelectedMode.Name,
        });
    }
}
