using Fennec.Shared.Dtos.Federation;

namespace Fennec.Api.FederationClient.Clients;

public interface IPresenceClient
{
    Task PushPresenceAsync(Guid userId, string username, bool isOnline, CancellationToken cancellationToken = default);
}

public class PresenceClient(HttpClient httpClient, string instanceUrl) : IPresenceClient
{
    public async Task PushPresenceAsync(Guid userId, string username, bool isOnline, CancellationToken cancellationToken = default)
    {
        var baseUri = instanceUrl.EndsWith('/') ? new Uri(instanceUrl) : new Uri(instanceUrl + "/");
        var uri = new Uri(baseUri, "federation/v1/presence/push");

        var response = await httpClient.PostAsJsonAsync(uri, new FederationPresencePushRequestDto
        {
            UserId = userId,
            Username = username,
            IsOnline = isOnline,
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
