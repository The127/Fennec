using System.Text.Json.Serialization;

namespace Fennec.Shared.Dtos.Server;

public class JoinServerRequestDto
{
    [JsonPropertyName("serverId")]
    public required Guid ServerId { get; set; }   
    
    [JsonPropertyName("instanceUrl")]
    public required string InstanceUrl { get; set; }
}