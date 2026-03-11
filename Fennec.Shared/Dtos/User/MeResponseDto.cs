using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.User;

public class MeResponseDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }
    
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; set; }
}