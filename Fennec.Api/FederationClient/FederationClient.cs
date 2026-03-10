using Fennec.Api.FederationClient.Clients;

namespace Fennec.Api.FederationClient;

public interface IFederationClient
{
    public ITargetedFederationClient For(string instanceUrl);
}

public class FederationClient(HttpClient httpClient) : IFederationClient
{
    public ITargetedFederationClient For(string instanceUrl)
    {
        return new TargetedFederationClient(httpClient, instanceUrl);
    }
}