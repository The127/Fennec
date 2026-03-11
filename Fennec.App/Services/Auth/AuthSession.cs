using System.Text.Json.Serialization;

namespace Fennec.App.Services.Auth;

public record AuthSession
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    
    [JsonPropertyName("sessionToken")]
    public required string SessionToken { get; init; }
    
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }
}