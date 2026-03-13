using Fennec.Shared.Dtos.Server;
using Fennec.Client;

namespace Fennec.App.Services;

public interface IServerStore
{
    Task<List<ListJoinedServersResponseItemDto>> GetJoinedServersAsync(string homeUrl, IFennecClient client, CancellationToken cancellationToken = default);
    
    Task<List<ListChannelGroupsResponseItemDto>> GetChannelGroupsAsync(string instanceUrl, IFennecClient client, Guid serverId, CancellationToken cancellationToken = default);
    
    Task<List<ListChannelsResponseItemDto>> GetChannelsAsync(string instanceUrl, IFennecClient client, Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default);
    
    Task WaitForRefreshesAsync();
}
