using Fennec.Api.Controllers.FederationApi;

namespace Fennec.Api.FederationClient.Clients;

public interface IServerClient
{
    Task<FederationServerController.ServerRedeemInviteFederateResponseDto> RedeemInviteAsync(
        FederationServerController.ServerRedeemInviteFederateRequestDto requestDto,
        CancellationToken cancellationToken = default);
}

public class ServerClient(HttpClient httpClient, string instanceUrl) : IServerClient
{
    public async Task<FederationServerController.ServerRedeemInviteFederateResponseDto> RedeemInviteAsync(
        FederationServerController.ServerRedeemInviteFederateRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(new Uri(instanceUrl), "federation/v1/server/invite/redeem");

        var response = await httpClient.PostAsJsonAsync(uri, requestDto, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseDto = await response.Content
            .ReadFromJsonAsync<FederationServerController.ServerRedeemInviteFederateResponseDto>(
                cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }
}
