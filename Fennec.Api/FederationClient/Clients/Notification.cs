using System.Net.Http.Json;
using Fennec.Api.Controllers.FederationApi;

namespace Fennec.Api.FederationClient.Clients;

public interface INotificationClient
{
    Task PushNotificationAsync(PushNotificationRequestDto requestDto, CancellationToken cancellationToken = default);
}

public class NotificationClient(HttpClient httpClient, string instanceUrl) : INotificationClient
{
    public async Task PushNotificationAsync(PushNotificationRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var baseUri = instanceUrl.EndsWith('/') ? new Uri(instanceUrl) : new Uri(instanceUrl + "/");
        var uri = new Uri(baseUri, "federation/v1/notification/push");

        var response = await httpClient.PostAsJsonAsync(uri, requestDto, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
