namespace Fennec.App.Messages;
public record VoiceMuteStateChangedMessage(Guid ServerId, Guid ChannelId, Guid UserId, bool IsMuted);
public record VoiceDeafenStateChangedMessage(Guid ServerId, Guid ChannelId, Guid UserId, bool IsDeafened);
public record VoiceSpeakingChangedMessage(Guid ServerId, Guid ChannelId, Guid UserId, bool IsSpeaking);
public record VoicePeerStateChangedMessage(Guid ServerId, Guid ChannelId, Guid UserId, string State);
public record ScreenShareStartedMessage(Guid ServerId, Guid ChannelId, Guid UserId, string Username, string? InstanceUrl);
public record ScreenShareStoppedMessage(Guid ServerId, Guid ChannelId, Guid UserId);
public record ScreenShareFrameMessage(Guid UserId, byte[] RgbaData, int Width, int Height, long Timestamp);
public record ScreenShareCursorMessage(Guid UserId, float X, float Y, CursorType Type, bool IsVisible);
public record ScreenShareWatcherAddedMessage(Guid ServerId, Guid ChannelId, Guid WatcherUserId);
public record ScreenShareWatcherRemovedMessage(Guid ServerId, Guid ChannelId, Guid WatcherUserId);
public record ScreenSharePopOutRequestedMessage(Guid UserId, string Username);
public record ScreenSharePopOutClosedMessage(Guid UserId);

public enum CursorType : byte
{
    Arrow,
    Hand,
    Text,
    Crosshair,
    ResizeNS,
    ResizeEW,
    ResizeNESW,
    ResizeNWSE,
    Move,
    NotAllowed,
    Wait,
    Help,
}
