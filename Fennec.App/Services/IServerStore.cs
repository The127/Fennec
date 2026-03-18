using Fennec.App.Domain;
using Fennec.Client;

namespace Fennec.App.Services;

public interface IServerStore
{
    Task<List<ServerSummary>> GetJoinedServersAsync(string homeUrl, IFennecClient client, CancellationToken cancellationToken = default);

    Task<List<ChannelGroupSummary>> GetChannelGroupsAsync(string instanceUrl, IFennecClient client, Guid serverId, CancellationToken cancellationToken = default);

    Task<List<ChannelSummary>> GetChannelsAsync(string instanceUrl, IFennecClient client, Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default);

    Task WaitForRefreshesAsync();
}
