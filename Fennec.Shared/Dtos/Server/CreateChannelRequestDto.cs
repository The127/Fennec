using System.Text.Json.Serialization;
using Fennec.Shared.Models;

namespace Fennec.Shared.Dtos.Server;

public class CreateChannelRequestDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("channelType")]
    public ChannelType? ChannelType { get; set; }
}
