using Fennec.Client.Clients;

namespace Fennec.Client;

public interface IFennecClient
{
    IAuthClient Auth { get; }
    IServerClient Server { get; }
    IUserClient User { get; }
}

public class FennecClient(HttpClient httpClient) : IFennecClient
{
    public IAuthClient Auth => new AuthClient(httpClient);
    public IServerClient Server => new ServerClient(httpClient);
    public IUserClient User => new UserClient(httpClient);   
}