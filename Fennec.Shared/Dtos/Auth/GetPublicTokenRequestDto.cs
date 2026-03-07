using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Auth;

public class GetPublicTokenRequestDto
{
    [JsonPropertyName("audience")]
    public required string Audience { get; set; }
}