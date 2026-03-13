using Fennec.Shared.Dtos.Server;

namespace Fennec.App.Messages;

public record ChannelMessageReceivedMessage(Guid ServerId, Guid ChannelId, MessageReceivedDto Message);
