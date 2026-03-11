namespace Fennec.Client;

public interface IClientFactory
{
    IFennecClient Create(string baseUrl, string? sessionToken = null);
}
