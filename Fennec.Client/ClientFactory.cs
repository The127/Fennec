namespace Fennec.Client;

public class ClientFactory(string baseUrl)
{
    public IFennecClient Create()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseUrl);
        
        return new FennecClient(httpClient);
    }
}