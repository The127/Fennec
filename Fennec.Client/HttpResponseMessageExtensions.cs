using System.Text.Json;

namespace Fennec.Client;

public class ApiException(string message) : Exception(message);

public static class HttpResponseMessageExtensions
{
    public static async Task EnsureSuccessAsync(this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = TryExtractMessage(body) ?? "An unexpected error occurred";
        throw new ApiException(message);
    }

    private static string? TryExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("description", out var desc))
                return desc.GetString();
        }
        catch (JsonException)
        {
            // Not JSON — use body as-is if it looks like a plain message.
        }

        return body.Length <= 200 ? body : null;
    }
}
