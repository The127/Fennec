using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class CreateServerInviteRequestDto
{
    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }

    [JsonPropertyName("maxUses")]
    public int? MaxUses { get; set; }
}
