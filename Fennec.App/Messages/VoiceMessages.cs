namespace Fennec.App.Messages;

public record VoiceParticipantJoinedMessage(Guid ServerId, Guid ChannelId, Guid UserId, string Username, string? InstanceUrl);
public record VoiceParticipantLeftMessage(Guid ServerId, Guid ChannelId, Guid UserId);
public record VoiceStateChangedMessage(bool IsConnected, Guid? ServerId, Guid? ChannelId);
public record VoiceMuteStateChangedMessage(Guid ServerId, Guid ChannelId, Guid UserId, bool IsMuted);
