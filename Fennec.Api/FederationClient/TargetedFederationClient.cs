using Fennec.Api.FederationClient.Clients;

namespace Fennec.Api.FederationClient;

public interface ITargetedFederationClient
{
    IServerClient Server { get; }
    INotificationClient Notification { get; }
}

public class TargetedFederationClient(HttpClient httpClient, string instanceUrl) : ITargetedFederationClient
{
    public IServerClient Server => new ServerClient(httpClient, instanceUrl);
    public INotificationClient Notification => new NotificationClient(httpClient, instanceUrl);
}