namespace Fennec.Client;

public class ClientFactory(string baseUrl)
{
    private string? _bearerToken;
    private string? _sessionToken;

    public ClientFactory WithBearerToken(string? token)
    {
        _bearerToken = token;
        return this;
    }

    public ClientFactory WithSessionToken(string? token)
    {
        _sessionToken = token;
        return this;
    }

    public IFennecClient Create()
    {
        var tokenProvider = new TokenProvider
        {
            BearerToken = _bearerToken,
            SessionToken = _sessionToken
        };

        var authHandler = new AuthHandler(tokenProvider)
        {
            InnerHandler = new HttpClientHandler()
        };
        
        var httpClient = new HttpClient(authHandler);
        httpClient.BaseAddress = new Uri(baseUrl);
        
        return new FennecClient(httpClient, tokenProvider);
    }
}