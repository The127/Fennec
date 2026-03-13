using Fennec.App.Services.Storage;
using Fennec.App.Services.Storage.Models;
using Fennec.Shared.Dtos.Server;
using Microsoft.EntityFrameworkCore;

namespace Fennec.App.Services;

public class ServerRepository(AppDbContext dbContext) : IServerRepository, IChannelGroupRepository, IChannelRepository
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

    public async Task<List<ListChannelGroupsResponseItemDto>> GetChannelGroupsAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ChannelGroups
            .Where(x => x.ServerId == serverId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ListChannelGroupsResponseItemDto
            {
                ChannelGroupId = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SetChannelGroupsAsync(Guid serverId, List<ListChannelGroupsResponseItemDto> groups, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ChannelGroups
            .Where(x => x.ServerId == serverId)
            .ToListAsync(cancellationToken);
        dbContext.ChannelGroups.RemoveRange(existing);

        var locals = groups.Select((g, i) => new LocalChannelGroup
        {
            Id = g.ChannelGroupId,
            Name = g.Name,
            ServerId = serverId,
            SortOrder = i
        });

        await dbContext.ChannelGroups.AddRangeAsync(locals, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ListChannelsResponseItemDto>> GetChannelsAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Channels
            .Where(x => x.ServerId == serverId && x.ChannelGroupId == channelGroupId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ListChannelsResponseItemDto
            {
                ChannelId = x.Id,
                Name = x.Name,
                ChannelGroupId = x.ChannelGroupId,
                ChannelType = x.ChannelType
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SetChannelsAsync(Guid serverId, Guid channelGroupId, List<ListChannelsResponseItemDto> channels, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Channels
            .Where(x => x.ServerId == serverId && x.ChannelGroupId == channelGroupId)
            .ToListAsync(cancellationToken);
        dbContext.Channels.RemoveRange(existing);

        var locals = channels.Select((c, i) => new LocalChannel
        {
            Id = c.ChannelId,
            Name = c.Name,
            ChannelGroupId = channelGroupId,
            ServerId = serverId,
            ChannelType = c.ChannelType,
            SortOrder = i
        });

        await dbContext.Channels.AddRangeAsync(locals, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
