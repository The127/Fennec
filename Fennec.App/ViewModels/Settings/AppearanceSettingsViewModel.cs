using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Services;
using Fennec.App.Themes;

namespace Fennec.App.ViewModels.Settings;

public partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;

    public IReadOnlyList<ThemeInfo> AvailableThemes => AppThemes.AllThemes;

    [ObservableProperty]
    private ThemeInfo _selectedTheme;

    public AppearanceSettingsViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        var app = Application.Current!;
        var current = app.RequestedThemeVariant;
        _selectedTheme = AppThemes.AllThemes.FirstOrDefault(t => t.Variant == current)
                         ?? AppThemes.AllThemes[0];
    }

    partial void OnSelectedThemeChanged(ThemeInfo value)
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = value.Variant;
        _ = _settingsStore.SaveAsync(new AppSettings { Theme = value.Name });
    }
}
