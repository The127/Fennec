using Fennec.App.Domain;
using Fennec.Shared.Dtos.Server;

namespace Fennec.App.Services;

public interface IServerRepository
{
    Task<List<ServerSummary>> GetJoinedServersAsync(CancellationToken cancellationToken = default);
    Task SetJoinedServersAsync(List<ListJoinedServersResponseItemDto> servers, CancellationToken cancellationToken = default);
    Task AddJoinedServerAsync(ListJoinedServersResponseItemDto server, CancellationToken cancellationToken = default);
    Task RemoveJoinedServerAsync(Guid serverId, CancellationToken cancellationToken = default);
}

public interface IChannelGroupRepository
{
    Task<List<ChannelGroupSummary>> GetChannelGroupsAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task SetChannelGroupsAsync(Guid serverId, List<ListChannelGroupsResponseItemDto> groups, CancellationToken cancellationToken = default);
}

public interface IChannelRepository
{
    Task<List<ChannelSummary>> GetChannelsAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default);
    Task SetChannelsAsync(Guid serverId, Guid channelGroupId, List<ListChannelsResponseItemDto> channels, CancellationToken cancellationToken = default);
}
