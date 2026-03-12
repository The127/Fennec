using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class CreateChannelGroupRequestDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
