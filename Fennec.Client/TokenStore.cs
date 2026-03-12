using System.Collections.Concurrent;

namespace Fennec.Client;

public class TokenStore : ITokenStore
{
    public string? HomeUrl { get; set; }
    public string? HomeSessionToken { get; set; }
    
    private readonly ConcurrentDictionary<string, (string Token, DateTime LastUsedUtc)> _publicTokens = new();

    public string? GetPublicToken(string targetUrl)
    {
        if (_publicTokens.TryGetValue(targetUrl, out var data))
        {
            return data.Token;
        }
        return null;
    }

    public void SetPublicToken(string targetUrl, string token)
    {
        _publicTokens[targetUrl] = (token, DateTime.UtcNow);
    }

    public void UpdateLastUsed(string targetUrl)
    {
        if (_publicTokens.TryGetValue(targetUrl, out var data))
        {
            _publicTokens[targetUrl] = (data.Token, DateTime.UtcNow);
        }
    }

    public IEnumerable<string> GetActiveTargets() => _publicTokens.Keys;

    public void RemoveTarget(string targetUrl) => _publicTokens.TryRemove(targetUrl, out _);

    public void CleanupIdle(TimeSpan idleTimeout)
    {
        var now = DateTime.UtcNow;
        foreach (var target in _publicTokens)
        {
            if (now - target.Value.LastUsedUtc > idleTimeout)
            {
                _publicTokens.TryRemove(target.Key, out _);
            }
        }
    }
}
