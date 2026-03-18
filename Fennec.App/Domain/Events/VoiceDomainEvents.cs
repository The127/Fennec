namespace Fennec.App.Domain.Events;

public record VoiceParticipantJoinedMessage(Guid ServerId, Guid ChannelId, Guid UserId, string Username, string? InstanceUrl, bool IsMuted = false, bool IsDeafened = false, bool IsScreenSharing = false) : IDomainEvent;
public record VoiceParticipantLeftMessage(Guid ServerId, Guid ChannelId, Guid UserId) : IDomainEvent;
public record VoiceStateChangedMessage(bool IsConnected, Guid? ServerId, Guid? ChannelId) : IDomainEvent;
