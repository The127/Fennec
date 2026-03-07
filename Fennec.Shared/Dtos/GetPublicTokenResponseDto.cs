using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos;

public class GetPublicTokenResponseDto
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }
}