using Fennec.Client.Clients;

namespace Fennec.Client;

public interface IFennecClient : IDisposable
{
    IAuthClient Auth { get; }
    IServerClient Server { get; }
    IUserClient User { get; }
    
    void SetHomeSession(string homeUrl, string sessionToken);
    void SetPublicToken(string targetUrl, string token);
}

public class FennecClient : IFennecClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStore _tokenStore;
    private readonly CancellationTokenSource _cts = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    public FennecClient(HttpClient httpClient, TokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        
        _ = BackgroundLoop(_cts.Token);
    }

    public IAuthClient Auth => new AuthClient(_httpClient);
    public IServerClient Server => new ServerClient(_httpClient);
    public IUserClient User => new UserClient(_httpClient);
    
    public void SetHomeSession(string homeUrl, string sessionToken)
    {
        _tokenStore.HomeUrl = homeUrl;
        _tokenStore.HomeSessionToken = sessionToken;
    }

    public void SetPublicToken(string targetUrl, string token) => _tokenStore.SetPublicToken(targetUrl, token);

    private async Task BackgroundLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);

                _tokenStore.CleanupIdle(TimeSpan.FromMinutes(15));

                if (DateTime.UtcNow - _lastRefresh >= TimeSpan.FromMinutes(3))
                {
                    await RefreshPublicTokensAsync(ct);
                    _lastRefresh = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Background refresh shouldn't crash the client
                Console.WriteLine($"[Fennec.Client] Error in background loop: {ex.Message}");
            }
        }
    }

    private async Task RefreshPublicTokensAsync(CancellationToken ct)
    {
        if (_tokenStore.HomeUrl == null || _tokenStore.HomeSessionToken == null) return;

        var targets = _tokenStore.GetActiveTargets().ToList();
        foreach (var target in targets)
        {
            if (target == _tokenStore.HomeUrl) continue;

            try
            {
                var response = await Auth.GetPublicTokenAsync(_tokenStore.HomeUrl, new Shared.Dtos.Auth.GetPublicTokenRequestDto
                {
                    Audience = target
                }, ct);

                _tokenStore.SetPublicToken(target, response.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fennec.Client] Failed to refresh token for {target}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _httpClient.Dispose();
    }
}