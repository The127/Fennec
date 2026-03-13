using Fennec.Api.Hubs;
using Fennec.Shared.Dtos.Server;
using MediatR;

namespace Fennec.Api.Events;

public record MessageReceivedEvent : INotification
{
    public required Guid ServerId { get; init; }
    public required Guid ChannelId { get; init; }
    public required MessageReceivedDto Message { get; init; }
}

public class SendNotificationOnMessageReceivedEventHandler(
    IMessageEventService messageEventService
) : INotificationHandler<MessageReceivedEvent>
{
    public async Task Handle(MessageReceivedEvent notification, CancellationToken cancellationToken)
    {
        await messageEventService.NotifyMessageReceived(
            notification.ServerId,
            notification.ChannelId,
            notification.Message,
            cancellationToken
        );
    }
}
