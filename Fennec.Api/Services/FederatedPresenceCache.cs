using System.Collections.Concurrent;

namespace Fennec.Api.Services;

public class FederatedPresenceCache
{
    private record CacheKey(Guid RemoteUserId, string InstanceUrl);

    private readonly ConcurrentDictionary<CacheKey, string> _online = new();

    public void SetPresence(Guid remoteUserId, string instanceUrl, string username, bool isOnline)
    {
        var key = new CacheKey(remoteUserId, instanceUrl);
        if (isOnline)
            _online[key] = username;
        else
            _online.TryRemove(key, out _);
    }

    public bool IsOnline(Guid remoteUserId, string instanceUrl)
    {
        return _online.ContainsKey(new CacheKey(remoteUserId, instanceUrl));
    }
}
