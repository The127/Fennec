using System.Text.Json.Serialization;
using Fennec.Shared.Models;

namespace Fennec.Shared.Dtos.Server;

public class UpdateChannelTypeRequestDto
{
    [JsonPropertyName("channelType")]
    public required ChannelType ChannelType { get; set; }
}
