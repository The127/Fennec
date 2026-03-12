using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListMessagesResponseDto
{
    [JsonPropertyName("messages")]
    public required List<ListMessagesResponseItemDto> Messages { get; set; }
}
