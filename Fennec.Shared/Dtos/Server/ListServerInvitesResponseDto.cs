using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListServerInvitesResponseDto
{
    [JsonPropertyName("invites")]
    public required List<ListServerInvitesResponseItemDto> Invites { get; set; }
}
