namespace Fennec.Client;

public class ClientFactory(TokenStore tokenStore) : IClientFactory
{
    public IFennecClient Create()
    {
        var authHandler = new AuthHandler(tokenStore)
        {
            InnerHandler = Ipv4HttpHandler.Create()
        };

        var httpClient = new HttpClient(authHandler);

        return new FennecClient(httpClient, tokenStore);
    }
}
