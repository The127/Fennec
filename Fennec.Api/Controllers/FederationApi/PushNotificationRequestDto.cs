using System.Text.Json.Serialization;
using Fennec.Api.Models;

namespace Fennec.Api.Controllers.FederationApi;

public record PushNotificationRequestDto
{
    [JsonPropertyName("targetUserId")]
    public required Guid TargetUserId { get; init; }

    [JsonPropertyName("type")]
    public required NotificationType Type { get; init; }

    [JsonPropertyName("serverId")]
    public required Guid ServerId { get; init; }

    [JsonPropertyName("channelId")]
    public required Guid ChannelId { get; init; }

    [JsonPropertyName("authorId")]
    public required Guid AuthorId { get; init; }

    [JsonPropertyName("authorName")]
    public required string AuthorName { get; init; }

    [JsonPropertyName("contentExcerpt")]
    public required string ContentExcerpt { get; init; }
}
