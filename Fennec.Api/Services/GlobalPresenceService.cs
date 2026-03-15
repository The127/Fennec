using System.Collections.Concurrent;

namespace Fennec.Api.Services;

public class GlobalPresenceService
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();
    private readonly ConcurrentDictionary<string, (Guid UserId, string Username)> _connectionInfo = new();
    private readonly object _lock = new();

    /// <summary>Registers a connection. Returns true if this is the user's first active connection.</summary>
    public bool AddConnection(Guid userId, string username, string connectionId)
    {
        _connectionInfo[connectionId] = (userId, username);
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var conns))
                _connections[userId] = conns = [];
            conns.Add(connectionId);
            return conns.Count == 1;
        }
    }

    /// <summary>Removes a connection by connectionId. Returns (userId, username, isLast) or null if not tracked.</summary>
    public (Guid UserId, string Username, bool IsLast)? RemoveConnection(string connectionId)
    {
        if (!_connectionInfo.TryRemove(connectionId, out var info))
            return null;

        lock (_lock)
        {
            if (!_connections.TryGetValue(info.UserId, out var conns))
                return (info.UserId, info.Username, true);

            conns.Remove(connectionId);
            if (conns.Count == 0)
            {
                _connections.TryRemove(info.UserId, out _);
                return (info.UserId, info.Username, true);
            }

            return (info.UserId, info.Username, false);
        }
    }
}
