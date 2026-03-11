namespace Fennec.Client;

public class ClientFactory(string baseUrl)
{
    private string? _bearerToken;
    private string? _sessionToken;

    private string NormalizedBaseUrl => baseUrl.Contains("://") ? baseUrl : $"https://{baseUrl}";

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
        httpClient.BaseAddress = new Uri(NormalizedBaseUrl);
        
        return new FennecClient(httpClient, tokenProvider);
    }
}