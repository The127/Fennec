using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListChannelGroupsResponseItemDto
{
    [JsonPropertyName("channelGroupId")]
    public required Guid ChannelGroupId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
