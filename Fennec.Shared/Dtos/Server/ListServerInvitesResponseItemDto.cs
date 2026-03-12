using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListServerInvitesResponseItemDto
{
    [JsonPropertyName("inviteId")]
    public required Guid InviteId { get; set; }

    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("createdByUserId")]
    public required Guid CreatedByUserId { get; set; }

    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }

    [JsonPropertyName("maxUses")]
    public int? MaxUses { get; set; }

    [JsonPropertyName("uses")]
    public required int Uses { get; set; }

    [JsonPropertyName("createdAt")]
    public required string CreatedAt { get; set; }
}
