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
}
