using System.Text.Json.Serialization;

namespace Fennec.Api.Controllers.FederationApi;

public record RemoteUserInfoDto
{
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}