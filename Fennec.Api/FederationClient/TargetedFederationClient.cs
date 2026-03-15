using Fennec.Api.FederationClient.Clients;

namespace Fennec.Api.FederationClient;

public interface ITargetedFederationClient
{
    IServerClient Server { get; }
    INotificationClient Notification { get; }
    IVoiceClient Voice { get; }
    IPresenceClient Presence { get; }
}

public class TargetedFederationClient(HttpClient httpClient, string instanceUrl) : ITargetedFederationClient
{
    public IServerClient Server => new ServerClient(httpClient, instanceUrl);
    public INotificationClient Notification => new NotificationClient(httpClient, instanceUrl);
    public IVoiceClient Voice => new VoiceClient(httpClient, instanceUrl);
    public IPresenceClient Presence => new PresenceClient(httpClient, instanceUrl);
}