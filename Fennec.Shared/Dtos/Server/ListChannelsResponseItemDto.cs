using System.Text.Json.Serialization;
using Fennec.Shared.Models;

namespace Fennec.Shared.Dtos.Server;

public class ListChannelsResponseItemDto
{
    [JsonPropertyName("channelId")]
    public required Guid ChannelId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("channelType")]
    public required ChannelType ChannelType { get; set; }

    [JsonPropertyName("channelGroupId")]
    public required Guid ChannelGroupId { get; set; }
}
