using Fennec.Api.Controllers.FederationApi;

namespace Fennec.Api.FederationClient.Clients;

public interface IServerClient
{
    Task<FederationServerController.ServerJoinFederateResponseDto> JoinServerAsync(FederationServerController.ServerJoinFederateRequestDto requestDto, CancellationToken cancellationToken = default);
}

public class ServerClient(HttpClient httpClient, string instanceUrl) : IServerClient
{
    public async Task<FederationServerController.ServerJoinFederateResponseDto> JoinServerAsync(FederationServerController.ServerJoinFederateRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(new Uri(instanceUrl), "federation/v1/server/join");
        
        var response = await httpClient.PostAsJsonAsync(uri, requestDto, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var responseDto = await response.Content.ReadFromJsonAsync<FederationServerController.ServerJoinFederateResponseDto>(cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }
}