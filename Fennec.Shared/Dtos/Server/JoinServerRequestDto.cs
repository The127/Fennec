using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class JoinServerRequestDto
{
    [JsonPropertyName("inviteCode")]
    public required string InviteCode { get; set; }

    [JsonPropertyName("instanceUrl")]
    public required string InstanceUrl { get; set; }
}
