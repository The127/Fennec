using Avalonia.Styling;

namespace Fennec.App.Themes;

public record ThemePalette(string Name);
public record ThemeMode(string Name, ThemeVariant BaseVariant);

public static class AppThemes
{
    public static readonly ThemePalette Default = new("Default");
    public static readonly ThemePalette Dracula = new("Dracula");
    public static readonly ThemePalette Nord = new("Nord");
    public static readonly ThemePalette Gruvbox = new("Gruvbox");
    public static readonly ThemePalette CatppuccinMocha = new("Catppuccin Mocha");

    public static readonly ThemeMode Dark = new("Dark", ThemeVariant.Dark);
    public static readonly ThemeMode Light = new("Light", ThemeVariant.Light);

    public static IReadOnlyList<ThemePalette> AllPalettes { get; } =
        [Default, Dracula, Nord, Gruvbox, CatppuccinMocha];

    public static IReadOnlyList<ThemeMode> AllModes { get; } = [Dark, Light];

    // Custom ThemeVariants keyed by "{Palette}-{Mode}"
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
            foreach (var mode in AllModes)
            {
                var key = $"{palette.Name}-{mode.Name}";
                dict[(palette.Name, mode.Name)] = new ThemeVariant(key, mode.BaseVariant);
            }
        }

        return dict;
    }

    public static ThemeVariant Resolve(string? paletteName, string? modeName)
    {
        var p = paletteName ?? "Default";
        var m = modeName ?? "Dark";
        return Variants.GetValueOrDefault((p, m), ThemeVariant.Dark);
    }

    public static ThemeVariant Resolve(ThemePalette palette, ThemeMode mode) =>
        Resolve(palette.Name, mode.Name);

    public static ThemePalette PaletteFromName(string? name) =>
        AllPalettes.FirstOrDefault(p => p.Name == name) ?? Default;

    public static ThemeMode ModeFromName(string? name) =>
        AllModes.FirstOrDefault(m => m.Name == name) ?? Dark;

    /// <summary>All custom ThemeVariant values for registration in AXAML ThemeDictionaries.</summary>
    public static IEnumerable<ThemeVariant> CustomVariants =>
        Variants.Values.Where(v => v != ThemeVariant.Dark && v != ThemeVariant.Light);
}
