using Fennec.App.Domain;
using Fennec.App.Services.Storage;
using Fennec.App.Services.Storage.Models;
using Fennec.Shared.Dtos.Server;
using Microsoft.EntityFrameworkCore;

namespace Fennec.App.Services;

public class ServerRepository(IDbContextFactory<AppDbContext> dbContextFactory) : IServerRepository, IChannelGroupRepository, IChannelRepository
{
    public async Task<List<ServerSummary>> GetJoinedServersAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var servers = await dbContext.Servers
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.JoinedAtUtc)
            .ToListAsync(cancellationToken);
        return servers.Select(x => new ServerSummary(x.Id, x.Name, x.InstanceUrl)).ToList();
    }

    public async Task SetJoinedServersAsync(List<ListJoinedServersResponseItemDto> servers, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Servers.ToDictionaryAsync(x => x.Id, cancellationToken);
        var toAdd = new List<LocalServer>();

        for (int i = 0; i < servers.Count; i++)
        {
            var s = servers[i];
            var summary = new ServerSummary(s.Id, s.Name, new InstanceUrl(s.InstanceUrl));
            if (existing.TryGetValue(s.Id, out var local))
            {
                local.UpdateFrom(summary, i);
                existing.Remove(s.Id);
            }
            else
            {
                var newServer = new LocalServer { Id = s.Id, Name = s.Name, InstanceUrl = new InstanceUrl(s.InstanceUrl), SortOrder = i };
                toAdd.Add(newServer);
            }
        }

        dbContext.Servers.RemoveRange(existing.Values);
        await dbContext.Servers.AddRangeAsync(toAdd, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddJoinedServerAsync(ListJoinedServersResponseItemDto server, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Servers.AnyAsync(x => x.Id == server.Id, cancellationToken))
            return;

        var maxSortOrder = await dbContext.Servers.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1;

        var local = new LocalServer
        {
            Id = server.Id,
            Name = server.Name,
            InstanceUrl = new InstanceUrl(server.InstanceUrl),
            SortOrder = maxSortOrder + 1
        };

        await dbContext.Servers.AddAsync(local, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveJoinedServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var server = await dbContext.Servers.FindAsync([serverId], cancellationToken);
        if (server != null)
        {
            dbContext.Servers.Remove(server);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<ChannelGroupSummary>> GetChannelGroupsAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChannelGroups
            .Where(x => x.ServerId == serverId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ChannelGroupSummary(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task SetChannelGroupsAsync(Guid serverId, List<ListChannelGroupsResponseItemDto> groups, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.ChannelGroups
            .Where(x => x.ServerId == serverId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var toAdd = new List<LocalChannelGroup>();

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var summary = new ChannelGroupSummary(g.ChannelGroupId, g.Name);
            if (existing.TryGetValue(g.ChannelGroupId, out var local))
            {
                local.UpdateFrom(summary, i);
                existing.Remove(g.ChannelGroupId);
            }
            else
            {
                toAdd.Add(new LocalChannelGroup { Id = g.ChannelGroupId, Name = g.Name, ServerId = serverId, SortOrder = i });
            }
        }

        dbContext.ChannelGroups.RemoveRange(existing.Values);
        await dbContext.ChannelGroups.AddRangeAsync(toAdd, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ChannelSummary>> GetChannelsAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Channels
            .Where(x => x.ServerId == serverId && x.ChannelGroupId == channelGroupId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ChannelSummary(x.Id, x.Name, x.ChannelType, x.ChannelGroupId))
            .ToListAsync(cancellationToken);
    }

    public async Task SetChannelsAsync(Guid serverId, Guid channelGroupId, List<ListChannelsResponseItemDto> channels, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Channels
            .Where(x => x.ServerId == serverId && x.ChannelGroupId == channelGroupId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var toAdd = new List<LocalChannel>();

        for (int i = 0; i < channels.Count; i++)
        {
            var c = channels[i];
            var summary = new ChannelSummary(c.ChannelId, c.Name, c.ChannelType, channelGroupId);
            if (existing.TryGetValue(c.ChannelId, out var local))
            {
                local.UpdateFrom(summary, i);
                existing.Remove(c.ChannelId);
            }
            else
            {
                toAdd.Add(new LocalChannel { Id = c.ChannelId, Name = c.Name, ChannelGroupId = channelGroupId, ServerId = serverId, ChannelType = c.ChannelType, SortOrder = i });
            }
        }

        dbContext.Channels.RemoveRange(existing.Values);
        await dbContext.Channels.AddRangeAsync(toAdd, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
