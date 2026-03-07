using System.Text.Json.Serialization;
using Fennec.Shared.Models;

namespace Fennec.Shared.Dtos;

public class CreateServerRequestDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("visibility")]
    public required ServerVisibility Visibility { get; set; }
}