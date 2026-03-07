using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos;

public class LoginRequestDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
        
    [JsonPropertyName("password")]
    public required string Password { get; set; }
}