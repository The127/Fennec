namespace Fennec.Client;

public class ClientFactory : IClientFactory
{
    public IFennecClient Create()
    {
        var tokenStore = new TokenStore();

        var authHandler = new AuthHandler(tokenStore)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(authHandler);

        return new FennecClient(httpClient, tokenStore);
    }
}