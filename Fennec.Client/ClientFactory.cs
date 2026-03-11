namespace Fennec.Client;

public class ClientFactory : IClientFactory
{
    private static string NormalizeBaseUrl(string baseUrl) => baseUrl.Contains("://")
        ? throw new ArgumentException("Base URL should not contain a scheme (e.g., http:// or https://).")
        : $"https://{baseUrl}";

    public IFennecClient Create(string baseUrl, string? sessionToken = null)
    {
        var tokenProvider = new TokenProvider
        {
            SessionToken = sessionToken
        };

        var authHandler = new AuthHandler(tokenProvider)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(authHandler);
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(baseUrl));

        return new FennecClient(httpClient, tokenProvider);
    }
}