using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListJoinedServersResponseDto
{
    [JsonPropertyName("servers")]
    public required List<ListJoinedServersResponseItemDto> Servers { get; init; }
}