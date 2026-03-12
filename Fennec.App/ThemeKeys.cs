using Avalonia.Styling;
using Fennec.App.Themes;

namespace Fennec.App;

/// <summary>
/// Static ThemeVariant references for use in AXAML ThemeDictionary keys.
/// </summary>
public static class ThemeKeys
{
    public static ThemeVariant Dracula => AppThemes.Dracula;
    public static ThemeVariant Nord => AppThemes.Nord;
    public static ThemeVariant Gruvbox => AppThemes.Gruvbox;
    public static ThemeVariant CatppuccinMocha => AppThemes.CatppuccinMocha;
}
