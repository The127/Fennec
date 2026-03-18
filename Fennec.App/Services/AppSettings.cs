using System.Text.Json;
using System.Text.Json.Serialization;
using Fennec.App.Domain;
using Fennec.App.Themes;
using AppThemeMode = Fennec.App.Themes.ThemeMode;

namespace Fennec.App.Services;

public class AppSettings
{
    [JsonPropertyName("theme")]
    [JsonConverter(typeof(ThemePaletteJsonConverter))]
    public ThemePalette Theme { get; set; } = AppThemes.Default;

    [JsonPropertyName("themeMode")]
    [JsonConverter(typeof(ThemeModeJsonConverter))]
    public AppThemeMode ThemeMode { get; set; } = AppThemes.Auto;

    [JsonPropertyName("zoomLevel")]
    public double ZoomLevel { get; set; } = 1.0;

    [JsonPropertyName("keyBindings")]
    public Dictionary<string, string>? KeyBindings { get; set; }

    [JsonPropertyName("mouseBindings")]
    public Dictionary<string, string>? MouseBindings { get; set; }

    [JsonPropertyName("audioHostApi")]
    public int? AudioHostApi { get; set; }

    [JsonPropertyName("inputDeviceName")]
    public string? InputDeviceName { get; set; }

    [JsonPropertyName("outputDeviceName")]
    public string? OutputDeviceName { get; set; }

    [JsonPropertyName("voiceSensitivity")]
    public double VoiceSensitivity { get; set; } = 0.015;

    [JsonPropertyName("voiceSoundsEnabled")]
    public bool VoiceSoundsEnabled { get; set; } = true;

    [JsonPropertyName("voiceSoundPack")]
    public string VoiceSoundPack { get; set; } = "Classic";

    [JsonPropertyName("screenShareResolution")]
    [JsonConverter(typeof(ScreenShareResolutionJsonConverter))]
    public ScreenShareResolution ScreenShareResolution { get; set; } = ScreenShareResolution.P1080;

    [JsonPropertyName("screenShareBitrateKbps")]
    public int ScreenShareBitrateKbps { get; set; } = 1500;

    [JsonPropertyName("screenShareFrameRate")]
    public int ScreenShareFrameRate { get; set; } = 30;

    [JsonPropertyName("viewerDownscalePercent")]
    public int ViewerDownscalePercent { get; set; } = 100;
}

public class ThemePaletteJsonConverter : JsonConverter<ThemePalette>
{
    public override ThemePalette Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => AppThemes.PaletteFromName(reader.GetString());

    public override void Write(Utf8JsonWriter writer, ThemePalette value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Name);
}

public class ThemeModeJsonConverter : JsonConverter<AppThemeMode>
{
    public override AppThemeMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => AppThemes.ModeFromName(reader.GetString());

    public override void Write(Utf8JsonWriter writer, AppThemeMode value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Name);
}

public class ScreenShareResolutionJsonConverter : JsonConverter<ScreenShareResolution>
{
    public override ScreenShareResolution Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ScreenShareResolution.FromValue(reader.GetString());

    public override void Write(Utf8JsonWriter writer, ScreenShareResolution value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
