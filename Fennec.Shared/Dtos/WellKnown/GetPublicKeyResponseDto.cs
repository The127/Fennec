using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.WellKnown;

public class GetPublicKeyResponseDto
{
    [JsonPropertyName("publicKeyPem")]
    public required string PublicKeyPem { get; set; }
}