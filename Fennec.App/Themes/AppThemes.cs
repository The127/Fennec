using Avalonia.Styling;

namespace Fennec.App.Themes;

public record ThemePalette(string Name);
public record ThemeMode(string Name);

public static class AppThemes
{
    public static readonly ThemePalette Default = new("Default");
    public static readonly ThemePalette Dracula = new("Dracula");
    public static readonly ThemePalette Nord = new("Nord");
    public static readonly ThemePalette Gruvbox = new("Gruvbox");
    public static readonly ThemePalette CatppuccinMocha = new("Catppuccin Mocha");

    public static readonly ThemeMode Auto = new("Auto");
    public static readonly ThemeMode Dark = new("Dark");
    public static readonly ThemeMode Light = new("Light");

    public static IReadOnlyList<ThemePalette> AllPalettes { get; } =
        [Default, Dracula, Nord, Gruvbox, CatppuccinMocha];

    public static IReadOnlyList<ThemeMode> AllModes { get; } = [Auto, Dark, Light];

    // Custom ThemeVariants keyed by "{Palette}-{Dark|Light}"
    private static readonly Dictionary<(string Palette, string Mode), ThemeVariant> Variants = BuildVariants();

    private static Dictionary<(string, string), ThemeVariant> BuildVariants()
    {
        var dict = new Dictionary<(string, string), ThemeVariant>
        {
            [("Default", "Dark")] = ThemeVariant.Dark,
            [("Default", "Light")] = ThemeVariant.Light,
        };

        foreach (var palette in AllPalettes)
        {
            if (palette == Default) continue;
            foreach (var effectiveMode in new[] { "Dark", "Light" })
            {
                var key = $"{palette.Name}-{effectiveMode}";
                var baseVariant = effectiveMode == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
                dict[(palette.Name, effectiveMode)] = new ThemeVariant(key, baseVariant);
            }
        }

        return dict;
    }

    /// <summary>
    /// Resolve palette + effective mode ("Dark" or "Light") to a ThemeVariant.
    /// For Auto mode, the caller must determine the effective mode from the OS first.
    /// </summary>
    public static ThemeVariant Resolve(string? paletteName, string? effectiveModeName)
    {
        var p = paletteName ?? "Default";
        var m = effectiveModeName ?? "Dark";
        return Variants.GetValueOrDefault((p, m), ThemeVariant.Dark);
    }

    public static ThemeVariant Resolve(ThemePalette palette, ThemeMode mode, ThemeVariant? osTheme = null)
    {
        var effectiveMode = ResolveEffectiveMode(mode, osTheme);
        return Resolve(palette.Name, effectiveMode);
    }

    /// <summary>
    /// Given a mode setting and the OS theme, return "Dark" or "Light".
    /// </summary>
    public static string ResolveEffectiveMode(ThemeMode mode, ThemeVariant? osTheme = null)
    {
        if (mode == Auto)
        {
            // Fall back to Dark if OS theme can't be determined
            return osTheme == ThemeVariant.Light ? "Light" : "Dark";
        }
        return mode.Name;
    }

    public static ThemePalette PaletteFromName(string? name) =>
        AllPalettes.FirstOrDefault(p => p.Name == name) ?? Default;

    public static ThemeMode ModeFromName(string? name) =>
        AllModes.FirstOrDefault(m => m.Name == name) ?? Auto;

    /// <summary>All custom ThemeVariant values for registration in AXAML ThemeDictionaries.</summary>
    public static IEnumerable<ThemeVariant> CustomVariants =>
        Variants.Values.Where(v => v != ThemeVariant.Dark && v != ThemeVariant.Light);
}
