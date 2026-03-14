using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ServerPresenceEntryDto
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("instanceUrl")]
    public string? InstanceUrl { get; set; }
}
