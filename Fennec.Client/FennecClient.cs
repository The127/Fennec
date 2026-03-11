using Fennec.Client.Clients;

namespace Fennec.Client;

public interface IFennecClient
{
    IAuthClient Auth { get; }
    IServerClient Server { get; }
    IUserClient User { get; }
    string BaseAddress { get; }
    
    void SetBearerToken(string? token);
    void SetSessionToken(string? token);
}

public class FennecClient(HttpClient httpClient, TokenProvider tokenProvider) : IFennecClient
{
    public IAuthClient Auth => new AuthClient(httpClient);
    public IServerClient Server => new ServerClient(httpClient);
    public IUserClient User => new UserClient(httpClient);
    public string BaseAddress => httpClient.BaseAddress?.ToString() ?? string.Empty;
    
    public void SetBearerToken(string? token) => tokenProvider.BearerToken = token;
    public void SetSessionToken(string? token) => tokenProvider.SessionToken = token;
}