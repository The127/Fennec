using Fennec.Client.Clients;

namespace Fennec.Client;

public interface IFennecClient
{
    IAuthClient Auth { get; }
}

public class FennecClient(HttpClient httpClient) : IFennecClient
{
    public IAuthClient Auth => new AuthClient(httpClient);
}