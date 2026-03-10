using Fennec.Api.Controllers.FederationApi;

namespace Fennec.Api.FederationClient.Clients;

public interface IServerClient
{
    Task JoinServerAsync(FederationServerController.ServerJoinFederateRequestDto requestDto, CancellationToken cancellationToken = default);
}

public class ServerClient(HttpClient httpClient, string instanceUrl) : IServerClient
{
    public async Task JoinServerAsync(FederationServerController.ServerJoinFederateRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(new Uri(instanceUrl), "federation/v1/server/join");
        
        var response = await httpClient.PostAsJsonAsync(uri, requestDto, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}