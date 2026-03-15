using System.Text.Json.Serialization;

namespace Fennec.App.Services;

public class AppSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Default";

    [JsonPropertyName("themeMode")]
    public string ThemeMode { get; set; } = "Auto";

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
    public string ScreenShareResolution { get; set; } = "1080p";

    [JsonPropertyName("screenShareBitrateKbps")]
    public int ScreenShareBitrateKbps { get; set; } = 1500;

    [JsonPropertyName("screenShareFrameRate")]
    public int ScreenShareFrameRate { get; set; } = 30;

    [JsonPropertyName("viewerDownscalePercent")]
    public int ViewerDownscalePercent { get; set; } = 100;
}
