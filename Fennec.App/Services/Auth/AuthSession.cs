using System.Text.Json.Serialization;

namespace Fennec.App.Services.Auth;

public record AuthSession
{
    [JsonPropertyName("sessionToken")]
    public required string SessionToken { get; init; }
    
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }   
}