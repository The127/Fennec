using System.Collections.Concurrent;
using Fennec.Shared.Dtos.Voice;

namespace Fennec.Api.Services;

public class VoiceStateService
{
    private readonly ConcurrentDictionary<(Guid ServerId, Guid ChannelId), List<(Guid UserId, string Username, string ConnectionId)>> _channels = new();
    private readonly ConcurrentDictionary<string, (Guid ServerId, Guid ChannelId, Guid UserId)> _connectionMap = new();
    private readonly object _lock = new();

    public List<VoiceParticipantDto> AddParticipant(Guid serverId, Guid channelId, Guid userId, string username, string connectionId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            var list = _channels.GetOrAdd(key, _ => []);

            // Remove existing entry for this user (reconnect scenario)
            list.RemoveAll(p => p.UserId == userId);

            list.Add((userId, username, connectionId));
            _connectionMap[connectionId] = (serverId, channelId, userId);

            return list.Select(p => new VoiceParticipantDto { UserId = p.UserId, Username = p.Username }).ToList();
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

            return info;
        }
    }

    public List<VoiceParticipantDto> GetParticipants(Guid serverId, Guid channelId)
    {
        lock (_lock)
        {
            var key = (serverId, channelId);
            if (_channels.TryGetValue(key, out var list))
                return list.Select(p => new VoiceParticipantDto { UserId = p.UserId, Username = p.Username }).ToList();
            return [];
        }
    }
}
