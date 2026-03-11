using Fennec.Client.Clients;

namespace Fennec.Client;

public interface IFennecClient
{
    IAuthClient Auth { get; }
    IServerClient Server { get; }
    IUserClient User { get; }
    
    void SetBearerToken(string? token);
    void SetSessionToken(string? token);
}

public class FennecClient(HttpClient httpClient, TokenProvider tokenProvider) : IFennecClient
{
    public IAuthClient Auth => new AuthClient(httpClient);
    public IServerClient Server => new ServerClient(httpClient);
    public IUserClient User => new UserClient(httpClient);
    
    public void SetBearerToken(string? token) => tokenProvider.BearerToken = token;
    public void SetSessionToken(string? token) => tokenProvider.SessionToken = token;
}