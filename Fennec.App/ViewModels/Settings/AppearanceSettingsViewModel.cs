using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services;

namespace Fennec.App.ViewModels.Settings;

public partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;

    [ObservableProperty]
    private bool _isDarkTheme;

    public AppearanceSettingsViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        var app = Application.Current!;
        _isDarkTheme = app.ActualThemeVariant == ThemeVariant.Dark;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
        _ = _settingsStore.SaveAsync(new AppSettings { Theme = value ? "Dark" : "Light" });
    }
}
