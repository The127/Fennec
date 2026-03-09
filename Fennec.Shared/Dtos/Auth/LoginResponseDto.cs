using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Auth;

public class LoginResponseDto
{
    [JsonPropertyName("sessionToken")]
    public required string SessionToken { get; set; }
    
    [JsonPropertyName("userId")]
    public required Guid UserId { get; set; }   
}