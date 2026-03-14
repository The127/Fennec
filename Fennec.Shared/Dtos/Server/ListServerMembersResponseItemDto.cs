using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListServerMembersResponseItemDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("instanceUrl")]
    public string? InstanceUrl { get; init; }
}
