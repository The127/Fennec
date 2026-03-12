using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class SendMessageResponseDto
{
    [JsonPropertyName("messageId")]
    public required Guid MessageId { get; set; }
}
