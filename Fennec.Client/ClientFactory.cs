namespace Fennec.Client;

public class ClientFactory(string baseUrl)
{
    private IFennecClient Create()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseUrl);
        
        return new FennecClient(httpClient);
    }
}