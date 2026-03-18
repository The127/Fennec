using Fennec.App.Services;
using Fennec.App.Services.Storage;
using Fennec.Shared.Dtos.Server;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Fennec.App.Tests.Services;

public class ServerRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ServerRepository _serverStore;

    public ServerRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContextFactory = new TestDbContextFactory(options);

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();

        _serverStore = new ServerRepository(_dbContextFactory);
    }

    [Fact]
    public async Task GetJoinedServersAsync_ReturnsEmpty_WhenNoServersExist()
    {
        var result = await _serverStore.GetJoinedServersAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task SetJoinedServersAsync_AddsServersToDatabase()
    {
        var servers = new List<ListJoinedServersResponseItemDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Server 1", InstanceUrl = "https://1.fennec.chat" },
            new() { Id = Guid.NewGuid(), Name = "Server 2", InstanceUrl = "https://2.fennec.chat" }
        };

        await _serverStore.SetJoinedServersAsync(servers);

        var result = await _serverStore.GetJoinedServersAsync();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Name == "Server 1");
        Assert.Contains(result, s => s.Name == "Server 2");
    }

    [Fact]
    public async Task GetChannelGroupsAsync_ReturnsEmpty_WhenNoGroupsExist()
    {
        var serverId = Guid.NewGuid();
        var result = await _serverStore.GetChannelGroupsAsync(serverId);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SetChannelGroupsAsync_AddsGroupsToDatabase()
    {
        var serverId = Guid.NewGuid();
        // Add server first to satisfy foreign key
        await _serverStore.AddJoinedServerAsync(new ListJoinedServersResponseItemDto
        {
            Id = serverId,
            Name = "Test Server",
            InstanceUrl = "https://fennec.chat"
        });

        var groups = new List<ListChannelGroupsResponseItemDto>
        {
            new() { ChannelGroupId = Guid.NewGuid(), Name = "Group 1" },
            new() { ChannelGroupId = Guid.NewGuid(), Name = "Group 2" }
        };

        await _serverStore.SetChannelGroupsAsync(serverId, groups);

        var result = await _serverStore.GetChannelGroupsAsync(serverId);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.Name == "Group 1");
        Assert.Contains(result, g => g.Name == "Group 2");
    }

    [Fact]
    public async Task GetChannelsAsync_ReturnsEmpty_WhenNoChannelsExist()
    {
        var serverId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var result = await _serverStore.GetChannelsAsync(serverId, groupId);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SetJoinedServersAsync_ReconcilesExistingServers()
    {
        // Arrange
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();
        
        await _serverStore.SetJoinedServersAsync(new List<ListJoinedServersResponseItemDto>
        {
            new() { Id = serverId1, Name = "Original Name 1", InstanceUrl = "https://1.fennec.chat" },
            new() { Id = serverId2, Name = "Original Name 2", InstanceUrl = "https://2.fennec.chat" }
        });

        var updatedServers = new List<ListJoinedServersResponseItemDto>
        {
            new() { Id = serverId1, Name = "Updated Name 1", InstanceUrl = "https://1.fennec.chat" },
            new() { Id = Guid.NewGuid(), Name = "New Server", InstanceUrl = "https://3.fennec.chat" }
        };

        // Act
        await _serverStore.SetJoinedServersAsync(updatedServers);

        // Assert
        var result = await _serverStore.GetJoinedServersAsync();
        Assert.Equal(2, result.Count);
        
        var s1 = result.Single(x => x.Id == serverId1);
        Assert.Equal("Updated Name 1", s1.Name);
        
        Assert.Contains(result, x => x.Name == "New Server");
        Assert.DoesNotContain(result, x => x.Id == serverId2);
    }

    [Fact]
    public async Task SetChannelGroupsAsync_ReconcilesExistingGroups()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var groupId1 = Guid.NewGuid();
        var groupId2 = Guid.NewGuid();

        await _serverStore.AddJoinedServerAsync(new ListJoinedServersResponseItemDto { Id = serverId, Name = "S", InstanceUrl = "U" });
        await _serverStore.SetChannelGroupsAsync(serverId, new List<ListChannelGroupsResponseItemDto>
        {
            new() { ChannelGroupId = groupId1, Name = "Group 1" },
            new() { ChannelGroupId = groupId2, Name = "Group 2" }
        });

        var updatedGroups = new List<ListChannelGroupsResponseItemDto>
        {
            new() { ChannelGroupId = groupId1, Name = "Updated Group 1" },
            new() { ChannelGroupId = Guid.NewGuid(), Name = "Group 3" }
        };

        // Act
        await _serverStore.SetChannelGroupsAsync(serverId, updatedGroups);

        // Assert
        var result = await _serverStore.GetChannelGroupsAsync(serverId);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.Name == "Updated Group 1");
        Assert.Contains(result, g => g.Name == "Group 3");
        Assert.DoesNotContain(result, g => g.Id == groupId2);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
