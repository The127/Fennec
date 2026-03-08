using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class CreateServerResponseDto
{
    [JsonPropertyName("serverId")]
    public Guid ServerId { get; set; }
}