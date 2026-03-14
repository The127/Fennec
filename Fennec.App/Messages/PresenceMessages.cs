namespace Fennec.App.Messages;

public record UserOnlineMessage(Guid ServerId, Guid UserId, string Username, string? InstanceUrl);
public record UserOfflineMessage(Guid ServerId, Guid UserId);
