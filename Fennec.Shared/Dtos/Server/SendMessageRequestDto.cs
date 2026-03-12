using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class SendMessageRequestDto
{
    [JsonPropertyName("content")]
    public required string Content { get; set; }
}
