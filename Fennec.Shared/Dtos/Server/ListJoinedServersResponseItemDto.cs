using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class ListJoinedServersResponseItemDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("instanceUrl")]
    public required string InstanceUrl { get; init; }   
}