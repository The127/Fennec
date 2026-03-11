using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Auth;

public class RegisterUserRequestDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("password")]
    public required string Password { get; set; }
    
    [JsonPropertyName("displayName")]
    public required string? DisplayName { get; set; }
}