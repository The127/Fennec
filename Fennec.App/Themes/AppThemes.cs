using Avalonia.Styling;

namespace Fennec.App.Themes;

public record ThemeInfo(string Name, ThemeVariant Variant);

public static class AppThemes
{
    public static readonly ThemeVariant Dark = ThemeVariant.Dark;
    public static readonly ThemeVariant Light = ThemeVariant.Light;
    public static readonly ThemeVariant Dracula = new("Dracula", ThemeVariant.Dark);
    public static readonly ThemeVariant Nord = new("Nord", ThemeVariant.Dark);
    public static readonly ThemeVariant Gruvbox = new("Gruvbox", ThemeVariant.Dark);
    public static readonly ThemeVariant CatppuccinMocha = new("CatppuccinMocha", ThemeVariant.Dark);

    public static IReadOnlyList<ThemeInfo> AllThemes { get; } =
    [
        new("Dark", Dark),
        new("Light", Light),
        new("Dracula", Dracula),
        new("Nord", Nord),
        new("Gruvbox", Gruvbox),
        new("Catppuccin Mocha", CatppuccinMocha),
    ];

    public static ThemeVariant FromName(string? name) =>
        AllThemes.FirstOrDefault(t => t.Name == name)?.Variant ?? Dark;
}
