using System.Collections.Concurrent;
using Fennec.Shared.Dtos.Voice;

namespace Fennec.Api.Services;

public class VoiceStateService
{
    private readonly ConcurrentDictionary<(Guid ServerId, Guid ChannelId), List<(Guid UserId, string Username, string? InstanceUrl, string? ConnectionId)>> _channels = new();
    private readonly ConcurrentDictionary<string, (Guid ServerId, Guid ChannelId, Guid UserId)> _connectionMap = new();
    private readonly ConcurrentDictionary<(Guid ServerId, Guid ChannelId), HashSet<Guid>> _screenSharers = new();
    private readonly ConcurrentDictionary<(Guid ServerId, Guid ChannelId), HashSet<Guid>> _mutedUsers = new();
    private readonly ConcurrentDictionary<(Guid ServerId, Guid ChannelId), HashSet<Guid>> _deafenedUsers = new();
    private readonly object _lock = new();

    public List<VoiceParticipantDto> AddParticipant(Guid serverId, Guid channelId, Guid userId, string username, string? instanceUrl, string? connectionId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            var list = _channels.GetOrAdd(key, _ => []);

            // Remove existing entry for this user (reconnect scenario)
            list.RemoveAll(p => p.UserId == userId);

            list.Add((userId, username, instanceUrl, connectionId));
            if (connectionId is not null)
                _connectionMap[connectionId] = (serverId, channelId, userId);

            return list.Select(p => ToDto(key, p)).ToList();
        }
    }

    public void RemoveParticipant(Guid serverId, Guid channelId, Guid userId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (_channels.TryGetValue(key, out var list))
            {
                var removed = list.FirstOrDefault(p => p.UserId == userId);
                list.RemoveAll(p => p.UserId == userId);
                if (removed.ConnectionId is not null)
                    _connectionMap.TryRemove(removed.ConnectionId, out _);
                if (list.Count == 0)
                    _channels.TryRemove(key, out _);
            }

            RemoveUserState(key, userId);
        }
    }

    public (Guid ServerId, Guid ChannelId, Guid UserId)? RemoveByConnectionId(string connectionId)
    {
        lock (_lock)
        {
            if (!_connectionMap.TryRemove(connectionId, out var info))
                return null;

            var key = (info.ServerId, info.ChannelId);
            if (_channels.TryGetValue(key, out var list))
            {
                list.RemoveAll(p => p.ConnectionId == connectionId);
                if (list.Count == 0)
                    _channels.TryRemove(key, out _);
            }

            RemoveUserState(key, info.UserId);

            return info;
        }
    }

    public string? GetConnectionId(Guid serverId, Guid channelId, Guid userId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (_channels.TryGetValue(key, out var list))
                return list.FirstOrDefault(p => p.UserId == userId).ConnectionId;
            return null;
        }
    }

    public List<VoiceParticipantDto> GetParticipants(Guid serverId, Guid channelId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (_channels.TryGetValue(key, out var list))
                return list.Select(p => ToDto(key, p)).ToList();
            return [];
        }
    }

    /// <summary>
    /// Returns the distinct instance URLs of remote participants in a channel (non-null, different from the given local instance URL).
    /// </summary>
    public List<string> GetRemoteInstanceUrls(Guid serverId, Guid channelId, string localInstanceUrl)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (_channels.TryGetValue(key, out var list))
                return list
                    .Where(p => p.InstanceUrl is not null && !p.InstanceUrl.Equals(localInstanceUrl, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.InstanceUrl!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            return [];
        }
    }

    public Dictionary<Guid, List<VoiceParticipantDto>> GetServerVoiceState(Guid serverId)
    {
        lock (_lock)
        {
            var result = new Dictionary<Guid, List<VoiceParticipantDto>>();
            foreach (var (key, list) in _channels)
            {
                if (key.ServerId == serverId && list.Count > 0)
                {
                    result[key.ChannelId] = list.Select(p => ToDto(key, p)).ToList();
                }
            }
            return result;
        }
    }

    public bool SetScreenSharing(Guid serverId, Guid channelId, Guid userId, bool isSharing)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);

            // Must be a participant to share
            if (!_channels.TryGetValue(key, out var list) || list.All(p => p.UserId != userId))
                return false;

            if (isSharing)
            {
                var sharers = _screenSharers.GetOrAdd(key, _ => []);
                sharers.Add(userId);
            }
            else
            {
                if (_screenSharers.TryGetValue(key, out var sharers))
                {
                    sharers.Remove(userId);
                    if (sharers.Count == 0)
                        _screenSharers.TryRemove(key, out _);
                }
            }

            return true;
        }
    }

    public List<Guid> GetScreenSharers(Guid serverId, Guid channelId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (_screenSharers.TryGetValue(key, out var sharers))
                return [.. sharers];
            return [];
        }
    }

    public bool IsScreenSharing(Guid serverId, Guid channelId, Guid userId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            return _screenSharers.TryGetValue(key, out var sharers) && sharers.Contains(userId);
        }
    }

    public void SetMuted(Guid serverId, Guid channelId, Guid userId, bool isMuted)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (isMuted)
            {
                var set = _mutedUsers.GetOrAdd(key, _ => []);
                set.Add(userId);
            }
            else
            {
                if (_mutedUsers.TryGetValue(key, out var set))
                {
                    set.Remove(userId);
                    if (set.Count == 0)
                        _mutedUsers.TryRemove(key, out _);
                }
            }
        }
    }

    public void SetDeafened(Guid serverId, Guid channelId, Guid userId, bool isDeafened)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (isDeafened)
            {
                var set = _deafenedUsers.GetOrAdd(key, _ => []);
                set.Add(userId);
            }
            else
            {
                if (_deafenedUsers.TryGetValue(key, out var set))
                {
                    set.Remove(userId);
                    if (set.Count == 0)
                        _deafenedUsers.TryRemove(key, out _);
                }
            }
        }
    }

    private void RemoveUserState((Guid ServerId, Guid ChannelId) key, Guid userId)
    {
        if (_screenSharers.TryGetValue(key, out var sharers))
        {
            sharers.Remove(userId);
            if (sharers.Count == 0)
                _screenSharers.TryRemove(key, out _);
        }
        if (_mutedUsers.TryGetValue(key, out var muted))
        {
            muted.Remove(userId);
            if (muted.Count == 0)
                _mutedUsers.TryRemove(key, out _);
        }
        if (_deafenedUsers.TryGetValue(key, out var deafened))
        {
            deafened.Remove(userId);
            if (deafened.Count == 0)
                _deafenedUsers.TryRemove(key, out _);
        }
    }

    private VoiceParticipantDto ToDto((Guid ServerId, Guid ChannelId) key, (Guid UserId, string Username, string? InstanceUrl, string? ConnectionId) p)
    {
        return new VoiceParticipantDto
        {
            UserId = p.UserId,
            Username = p.Username,
            InstanceUrl = p.InstanceUrl,
            IsMuted = _mutedUsers.TryGetValue(key, out var muted) && muted.Contains(p.UserId),
            IsDeafened = _deafenedUsers.TryGetValue(key, out var deafened) && deafened.Contains(p.UserId),
            IsScreenSharing = _screenSharers.TryGetValue(key, out var sharers) && sharers.Contains(p.UserId),
        };
    }
}
