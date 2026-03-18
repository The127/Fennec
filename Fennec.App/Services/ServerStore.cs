using Fennec.App.Domain;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

public class ServerStore(
    IServerRepository serverRepo,
    IChannelGroupRepository groupRepo,
    IChannelRepository channelRepo,
    ILogger<ServerStore> logger) : IServerStore
{
    private readonly Dictionary<string, Task> _pendingRefreshes = [];
    private readonly object _lock = new();

    public Task WaitForRefreshesAsync()
    {
        List<Task> tasks;
        lock (_lock)
        {
            tasks = _pendingRefreshes.Values.ToList();
        }
        return Task.WhenAll(tasks);
    }

    private Task TrackRefresh(string key, Func<Task> action, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_pendingRefreshes.TryGetValue(key, out var existing) && !existing.IsCompleted)
            {
                return existing;
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    await action();
                }
                finally
                {
                    lock (_lock)
                    {
                        if (_pendingRefreshes.TryGetValue(key, out var current) && current.Id == Task.CurrentId)
                        {
                            _pendingRefreshes.Remove(key);
                        }
                    }
                }
            }, cancellationToken);

            _pendingRefreshes[key] = task;
            return task;
        }
    }

    public async Task<List<ServerSummary>> GetJoinedServersAsync(string homeUrl, IFennecClient client, CancellationToken cancellationToken = default)
    {
        var cached = await serverRepo.GetJoinedServersAsync(cancellationToken);

        var refreshTask = TrackRefresh($"servers:{homeUrl}", async () =>
        {
            try
            {
                var response = await client.Server.ListJoinedServersAsync(homeUrl, cancellationToken);
                await serverRepo.SetJoinedServersAsync(response.Servers, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to refresh joined servers from {Url}", homeUrl);
            }
        }, cancellationToken);

        if (cached.Count == 0)
        {
            await refreshTask;
            return await serverRepo.GetJoinedServersAsync(cancellationToken);
        }

        return cached;
    }

    public async Task<List<ChannelGroupSummary>> GetChannelGroupsAsync(string instanceUrl, IFennecClient client, Guid serverId, CancellationToken cancellationToken = default)
    {
        var cached = await groupRepo.GetChannelGroupsAsync(serverId, cancellationToken);

        var refreshTask = TrackRefresh($"groups:{serverId}", async () =>
        {
            try
            {
                var response = await client.Server.ListChannelGroupsAsync(instanceUrl, serverId, cancellationToken);
                await groupRepo.SetChannelGroupsAsync(serverId, response.ChannelGroups, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to refresh channel groups for server {ServerId} from {Url}", serverId, instanceUrl);
            }
        }, cancellationToken);

        if (cached.Count == 0)
        {
            await refreshTask;
            return await groupRepo.GetChannelGroupsAsync(serverId, cancellationToken);
        }

        return cached;
    }

    public async Task<List<ChannelSummary>> GetChannelsAsync(string instanceUrl, IFennecClient client, Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default)
    {
        var cached = await channelRepo.GetChannelsAsync(serverId, channelGroupId, cancellationToken);

        var refreshTask = TrackRefresh($"channels:{channelGroupId}", async () =>
        {
            try
            {
                var response = await client.Server.ListChannelsAsync(instanceUrl, serverId, channelGroupId, cancellationToken);
                await channelRepo.SetChannelsAsync(serverId, channelGroupId, response.Channels, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to refresh channels for group {GroupId} from {Url}", channelGroupId, instanceUrl);
            }
        }, cancellationToken);

        if (cached.Count == 0)
        {
            await refreshTask;
            return await channelRepo.GetChannelsAsync(serverId, channelGroupId, cancellationToken);
        }

        return cached;
    }
}
