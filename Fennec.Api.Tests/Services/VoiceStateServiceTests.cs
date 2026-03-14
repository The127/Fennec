using Fennec.Api.Services;

namespace Fennec.Api.Tests.Services;

public class VoiceStateServiceTests
{
    private readonly VoiceStateService _sut = new();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _channelId = Guid.NewGuid();

    [Fact]
    public void AddParticipant_with_null_connectionId_does_not_track_in_connectionMap()
    {
        var userId = Guid.NewGuid();

        _sut.AddParticipant(_serverId, _channelId, userId, "user1", "https://remote.instance", connectionId: null);

        // RemoveByConnectionId should not find this participant
        var removed = _sut.RemoveByConnectionId("any-connection-id");
        Assert.Null(removed);

        // But the participant should be in the channel
        var participants = _sut.GetParticipants(_serverId, _channelId);
        Assert.Single(participants);
        Assert.Equal(userId, participants[0].UserId);
    }

    [Fact]
    public void RemoveParticipant_removes_remote_participant_by_userId()
    {
        var userId = Guid.NewGuid();

        _sut.AddParticipant(_serverId, _channelId, userId, "user1", "https://remote.instance", connectionId: null);
        _sut.RemoveParticipant(_serverId, _channelId, userId);

        var participants = _sut.GetParticipants(_serverId, _channelId);
        Assert.Empty(participants);
    }

    [Fact]
    public void GetRemoteInstanceUrls_returns_distinct_remote_urls()
    {
        var localUrl = "https://local.instance";

        _sut.AddParticipant(_serverId, _channelId, Guid.NewGuid(), "local1", localUrl, "conn1");
        _sut.AddParticipant(_serverId, _channelId, Guid.NewGuid(), "remote1", "https://remote-a.instance", connectionId: null);
        _sut.AddParticipant(_serverId, _channelId, Guid.NewGuid(), "remote2", "https://remote-a.instance", connectionId: null);
        _sut.AddParticipant(_serverId, _channelId, Guid.NewGuid(), "remote3", "https://remote-b.instance", connectionId: null);

        var urls = _sut.GetRemoteInstanceUrls(_serverId, _channelId, localUrl);

        Assert.Equal(2, urls.Count);
        Assert.Contains("https://remote-a.instance", urls);
        Assert.Contains("https://remote-b.instance", urls);
    }

    [Fact]
    public void AddParticipant_with_connectionId_allows_RemoveByConnectionId()
    {
        var userId = Guid.NewGuid();
        var connectionId = "conn-123";

        _sut.AddParticipant(_serverId, _channelId, userId, "user1", null, connectionId);

        var removed = _sut.RemoveByConnectionId(connectionId);

        Assert.NotNull(removed);
        Assert.Equal(_serverId, removed.Value.ServerId);
        Assert.Equal(_channelId, removed.Value.ChannelId);
        Assert.Equal(userId, removed.Value.UserId);
    }

    [Fact]
    public void AddParticipant_replaces_existing_entry_for_same_user()
    {
        var userId = Guid.NewGuid();

        _sut.AddParticipant(_serverId, _channelId, userId, "user1", null, "conn1");
        _sut.AddParticipant(_serverId, _channelId, userId, "user1-updated", null, "conn2");

        var participants = _sut.GetParticipants(_serverId, _channelId);
        Assert.Single(participants);
        Assert.Equal("user1-updated", participants[0].Username);
    }
}
