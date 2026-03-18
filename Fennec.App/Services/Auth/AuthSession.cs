using System.Text.Json;
using System.Text.Json.Serialization;
using Fennec.App.Domain;

namespace Fennec.App.Services.Auth;

public class InstanceUrlJsonConverter : JsonConverter<InstanceUrl>
{
    public override InstanceUrl Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, InstanceUrl value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

public record AuthSession
{
    [JsonPropertyName("url")]
    [JsonConverter(typeof(InstanceUrlJsonConverter))]
    public required InstanceUrl Url { get; init; }
    
    [JsonPropertyName("sessionToken")]
    public required string SessionToken { get; init; }
    
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }
}