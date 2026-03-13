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
        var existing = await dbContext.Servers.ToDictionaryAsync(x => x.Id, cancellationToken);
        var toAdd = new List<LocalServer>();
        
        for (int i = 0; i < servers.Count; i++)
        {
            var s = servers[i];
            if (existing.TryGetValue(s.Id, out var local))
            {
                local.Name = s.Name;
                local.InstanceUrl = s.InstanceUrl;
                local.SortOrder = i;
                existing.Remove(s.Id);
            }
            else
            {
                toAdd.Add(new LocalServer
                {
                    Id = s.Id,
                    Name = s.Name,
                    InstanceUrl = s.InstanceUrl,
                    SortOrder = i
                });
            }
        }
        
        dbContext.Servers.RemoveRange(existing.Values);
        await dbContext.Servers.AddRangeAsync(toAdd, cancellationToken);
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
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        
        var toAdd = new List<LocalChannelGroup>();

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (existing.TryGetValue(g.ChannelGroupId, out var local))
            {
                local.Name = g.Name;
                local.SortOrder = i;
                existing.Remove(g.ChannelGroupId);
            }
            else
            {
                toAdd.Add(new LocalChannelGroup
                {
                    Id = g.ChannelGroupId,
                    Name = g.Name,
                    ServerId = serverId,
                    SortOrder = i
                });
            }
        }

        dbContext.ChannelGroups.RemoveRange(existing.Values);
        await dbContext.ChannelGroups.AddRangeAsync(toAdd, cancellationToken);
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
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        
        var toAdd = new List<LocalChannel>();

        for (int i = 0; i < channels.Count; i++)
        {
            var c = channels[i];
            if (existing.TryGetValue(c.ChannelId, out var local))
            {
                local.Name = c.Name;
                local.ChannelType = c.ChannelType;
                local.SortOrder = i;
                existing.Remove(c.ChannelId);
            }
            else
            {
                toAdd.Add(new LocalChannel
                {
                    Id = c.ChannelId,
                    Name = c.Name,
                    ChannelGroupId = channelGroupId,
                    ServerId = serverId,
                    ChannelType = c.ChannelType,
                    SortOrder = i
                });
            }
        }

        dbContext.Channels.RemoveRange(existing.Values);
        await dbContext.Channels.AddRangeAsync(toAdd, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
