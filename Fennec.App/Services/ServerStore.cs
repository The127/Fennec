using Fennec.App.Services.Storage;
using Fennec.App.Services.Storage.Models;
using Fennec.Shared.Dtos.Server;
using Microsoft.EntityFrameworkCore;

namespace Fennec.App.Services;

public class ServerStore(AppDbContext dbContext) : IServerStore
{
    public async Task<List<ListJoinedServersResponseItemDto>> GetJoinedServersAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Servers
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.JoinedAtUtc)
            .Select(x => new ListJoinedServersResponseItemDto
            {
                Id = x.Id,
                Name = x.Name,
                InstanceUrl = x.InstanceUrl
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SetJoinedServersAsync(List<ListJoinedServersResponseItemDto> servers, CancellationToken cancellationToken = default)
    {
        // For a full set, we clear and re-add or reconcile. 
        // In this simple store, we'll reconcile.
        var existing = await dbContext.Servers.ToListAsync(cancellationToken);
        dbContext.Servers.RemoveRange(existing);
        
        var locals = servers.Select((s, i) => new LocalServer
        {
            Id = s.Id,
            Name = s.Name,
            InstanceUrl = s.InstanceUrl,
            SortOrder = i
        });
        
        await dbContext.Servers.AddRangeAsync(locals, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddJoinedServerAsync(ListJoinedServersResponseItemDto server, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Servers.AnyAsync(x => x.Id == server.Id, cancellationToken))
            return;

        var maxSortOrder = await dbContext.Servers.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1;

        var local = new LocalServer
        {
            Id = server.Id,
            Name = server.Name,
            InstanceUrl = server.InstanceUrl,
            SortOrder = maxSortOrder + 1
        };

        await dbContext.Servers.AddAsync(local, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveJoinedServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await dbContext.Servers.FindAsync([serverId], cancellationToken);
        if (server != null)
        {
            dbContext.Servers.Remove(server);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
