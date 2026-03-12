using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListChannelsResponseDto
{
    [JsonPropertyName("channels")]
    public required List<ListChannelsResponseItemDto> Channels { get; set; }
}
