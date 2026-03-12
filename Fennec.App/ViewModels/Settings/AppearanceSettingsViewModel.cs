using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fennec.App.ViewModels.Settings;

public partial class AppearanceSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDarkTheme;

    public AppearanceSettingsViewModel()
    {
        var app = Application.Current!;
        _isDarkTheme = app.ActualThemeVariant == ThemeVariant.Dark;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
