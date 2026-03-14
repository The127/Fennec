using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListServerMembersResponseDto
{
    [JsonPropertyName("members")]
    public required List<ListServerMembersResponseItemDto> Members { get; init; }
}
