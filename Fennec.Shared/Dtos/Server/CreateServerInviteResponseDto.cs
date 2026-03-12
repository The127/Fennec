using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class CreateServerInviteResponseDto
{
    [JsonPropertyName("inviteId")]
    public required Guid InviteId { get; set; }

    [JsonPropertyName("code")]
    public required string Code { get; set; }
}
