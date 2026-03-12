using System.Text.Json.Serialization;

namespace Fennec.App.Services;

public class AppSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Dark";
}
