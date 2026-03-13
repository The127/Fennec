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

    [JsonPropertyName("audioHostApi")]
    public int? AudioHostApi { get; set; }

    [JsonPropertyName("inputDeviceName")]
    public string? InputDeviceName { get; set; }

    [JsonPropertyName("outputDeviceName")]
    public string? OutputDeviceName { get; set; }
}
