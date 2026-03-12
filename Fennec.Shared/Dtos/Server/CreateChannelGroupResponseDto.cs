using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class CreateChannelGroupResponseDto
{
    [JsonPropertyName("channelGroupId")]
    public required Guid ChannelGroupId { get; set; }
}
