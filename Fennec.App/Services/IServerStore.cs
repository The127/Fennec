using Fennec.Shared.Dtos.Server;

namespace Fennec.App.Services;

public interface IServerStore
{
    Task<List<ListJoinedServersResponseItemDto>> GetJoinedServersAsync(CancellationToken cancellationToken = default);
    Task SetJoinedServersAsync(List<ListJoinedServersResponseItemDto> servers, CancellationToken cancellationToken = default);
    Task AddJoinedServerAsync(ListJoinedServersResponseItemDto server, CancellationToken cancellationToken = default);
    Task RemoveJoinedServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<List<ListChannelGroupsResponseItemDto>> GetChannelGroupsAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task SetChannelGroupsAsync(Guid serverId, List<ListChannelGroupsResponseItemDto> groups, CancellationToken cancellationToken = default);

    Task<List<ListChannelsResponseItemDto>> GetChannelsAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default);
    Task SetChannelsAsync(Guid serverId, Guid channelGroupId, List<ListChannelsResponseItemDto> channels, CancellationToken cancellationToken = default);
}
