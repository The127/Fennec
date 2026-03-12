using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListChannelGroupsResponseDto
{
    [JsonPropertyName("channelGroups")]
    public required List<ListChannelGroupsResponseItemDto> ChannelGroups { get; set; }
}
