using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListMessagesResponseItemDto
{
    [JsonPropertyName("messageId")]
    public required Guid MessageId { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [JsonPropertyName("authorId")]
    public required Guid AuthorId { get; set; }

    [JsonPropertyName("authorName")]
    public required string AuthorName { get; set; }

    [JsonPropertyName("createdAt")]
    public required string CreatedAt { get; set; }
}
