using Avalonia.Styling;
using Fennec.App.Themes;

namespace Fennec.App;

/// <summary>
/// Static ThemeVariant references for use in AXAML ThemeDictionary keys.
/// </summary>
public static class ThemeKeys
{
    public static ThemeVariant DraculaDark => AppThemes.Resolve(AppThemes.Dracula, AppThemes.Dark);
    public static ThemeVariant DraculaLight => AppThemes.Resolve(AppThemes.Dracula, AppThemes.Light);
    public static ThemeVariant NordDark => AppThemes.Resolve(AppThemes.Nord, AppThemes.Dark);
    public static ThemeVariant NordLight => AppThemes.Resolve(AppThemes.Nord, AppThemes.Light);
    public static ThemeVariant GruvboxDark => AppThemes.Resolve(AppThemes.Gruvbox, AppThemes.Dark);
    public static ThemeVariant GruvboxLight => AppThemes.Resolve(AppThemes.Gruvbox, AppThemes.Light);
    public static ThemeVariant CatppuccinMochaDark => AppThemes.Resolve(AppThemes.CatppuccinMocha, AppThemes.Dark);
    public static ThemeVariant CatppuccinMochaLight => AppThemes.Resolve(AppThemes.CatppuccinMocha, AppThemes.Light);
}
