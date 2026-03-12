using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class CreateChannelResponseDto
{
    [JsonPropertyName("channelId")]
    public required Guid ChannelId { get; set; }
}
