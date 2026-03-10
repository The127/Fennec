using Fennec.Api.FederationClient.Clients;

namespace Fennec.Api.FederationClient;

public interface ITargetedFederationClient
{
    IServerClient Server { get; }
}

public class TargetedFederationClient(HttpClient httpClient, string instanceUrl) : ITargetedFederationClient
{
    public IServerClient Server => new ServerClient(httpClient, instanceUrl);
}