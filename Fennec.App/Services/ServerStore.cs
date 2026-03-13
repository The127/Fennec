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
    public async Task<List<ListJoinedServersResponseItemDto>> GetJoinedServersAsync(string homeUrl, IFennecClient client, CancellationToken cancellationToken = default)
    {
        var cached = await serverRepo.GetJoinedServersAsync(cancellationToken);
        
        _ = Task.Run(async () =>
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

        return cached;
    }

    public async Task<List<ListChannelGroupsResponseItemDto>> GetChannelGroupsAsync(string instanceUrl, IFennecClient client, Guid serverId, CancellationToken cancellationToken = default)
    {
        var cached = await groupRepo.GetChannelGroupsAsync(serverId, cancellationToken);

        _ = Task.Run(async () =>
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

        return cached;
    }

    public async Task<List<ListChannelsResponseItemDto>> GetChannelsAsync(string instanceUrl, IFennecClient client, Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default)
    {
        var cached = await channelRepo.GetChannelsAsync(serverId, channelGroupId, cancellationToken);

        _ = Task.Run(async () =>
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

        return cached;
    }
}
