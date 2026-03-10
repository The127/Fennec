using System.Text.Json.Serialization;
using Fennec.Shared.Models;

namespace Fennec.Shared.Dtos.Server;

public class ListServerResponseDto
{
    [JsonPropertyName("servers")]
    public required List<ListServerResponseItemDto> Servers { get; init; }
}