using Fennec.App.Services;
using Fennec.App.Services.Storage;
using Fennec.Shared.Dtos.Server;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fennec.App.Tests.Services;

public class ServerRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly ServerRepository _serverStore;

    public ServerRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _serverStore = new ServerRepository(_dbContext);
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
    public async Task SetJoinedServersAsync_ConcurrentCalls_DoesNotCreateDuplicates()
    {
        var serverId = Guid.NewGuid();
        var servers = new List<ListJoinedServersResponseItemDto>
        {
            new() { Id = serverId, Name = "Server 1", InstanceUrl = "https://1.fennec.chat" }
        };

        // Simulate concurrent calls. 
        // Note: Using the same DbContext instance for both calls might not fully simulate 
        // real-world concurrency if the app uses different Scopes, 
        // but here ServerStore is a Singleton and DbContext is registered with AddDbContext 
        // (which is scoped by default, but App.axaml.cs registrations might be different).
        // In App.axaml.cs, IServerStore is Singleton.
        
        var task1 = _serverStore.SetJoinedServersAsync(servers);
        var task2 = _serverStore.SetJoinedServersAsync(servers);

        await Task.WhenAll(task1, task2);

        var result = await _serverStore.GetJoinedServersAsync();
        Assert.Single(result);
        Assert.Equal(serverId, result[0].Id);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
