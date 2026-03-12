using Fennec.Shared.Dtos.Server;

namespace Fennec.App.Services;

public interface IServerStore
{
    Task<List<ListJoinedServersResponseItemDto>> GetJoinedServersAsync(CancellationToken cancellationToken = default);
    Task SetJoinedServersAsync(List<ListJoinedServersResponseItemDto> servers, CancellationToken cancellationToken = default);
    Task AddJoinedServerAsync(ListJoinedServersResponseItemDto server, CancellationToken cancellationToken = default);
    Task RemoveJoinedServerAsync(Guid serverId, CancellationToken cancellationToken = default);
}
