using System.Collections.Concurrent;

namespace Fennec.Api.Services;

public class PresenceService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, PresenceEntry>> _serverPresence = new();

    public record PresenceEntry(Guid UserId, string Username, string? InstanceUrl, string ConnectionId);

    public bool AddUser(Guid serverId, Guid userId, string username, string? instanceUrl, string connectionId)
    {
        var serverUsers = _serverPresence.GetOrAdd(serverId, _ => new());
        var added = serverUsers.TryAdd(connectionId, new PresenceEntry(userId, username, instanceUrl, connectionId));

        // Return true only if this is the first connection for this user on this server
        if (added)
        {
            var isFirstConnection = serverUsers.Values.Count(e => e.UserId == userId) == 1;
            return isFirstConnection;
        }

        return false;
    }

    public (bool wasLastConnection, Guid serverId, Guid userId, string username, string? instanceUrl)? RemoveByConnectionId(string connectionId)
    {
        foreach (var (serverId, serverUsers) in _serverPresence)
        {
            if (serverUsers.TryRemove(connectionId, out var entry))
            {
                var wasLast = !serverUsers.Values.Any(e => e.UserId == entry.UserId);
                return (wasLast, serverId, entry.UserId, entry.Username, entry.InstanceUrl);
            }
        }

        return null;
    }

    public List<(Guid serverId, Guid userId, string username, string? instanceUrl)> RemoveAllByConnectionId(string connectionId)
    {
        var results = new List<(Guid, Guid, string, string?)>();

        foreach (var (serverId, serverUsers) in _serverPresence)
        {
            if (serverUsers.TryRemove(connectionId, out var entry))
            {
                var wasLast = !serverUsers.Values.Any(e => e.UserId == entry.UserId);
                if (wasLast)
                    results.Add((serverId, entry.UserId, entry.Username, entry.InstanceUrl));
            }
        }

        return results;
    }

    public List<PresenceEntry> GetOnlineUsers(Guid serverId)
    {
        if (!_serverPresence.TryGetValue(serverId, out var serverUsers))
            return [];

        return serverUsers.Values
            .GroupBy(e => e.UserId)
            .Select(g => g.First())
            .ToList();
    }
}
