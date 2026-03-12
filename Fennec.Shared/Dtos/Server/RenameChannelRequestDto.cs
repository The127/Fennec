using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class RenameChannelRequestDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
