namespace Fennec.App.Messages;

public record ControlNavigateToServerMessage(Guid ServerId);
public record ControlWatchScreenShareMessage(Guid UserId);
public record ControlUnwatchScreenShareMessage(Guid UserId);
