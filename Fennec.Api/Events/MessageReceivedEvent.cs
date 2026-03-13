using Fennec.Api.Hubs;
using Fennec.Api.Models;
using Fennec.Api.Services;
using Fennec.Shared.Dtos.Server;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Events;

public record MessageReceivedEvent : INotification
{
    public required Guid ServerId { get; init; }
    public required Guid ChannelId { get; init; }
    public required MessageReceivedDto Message { get; init; }
}

public class NotifyActiveChannelUsersOnMessageReceivedEventHandler(
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

public class ProcessNotificationsOnMessageReceivedEventHandler(
    INotificationService notificationService,
    FennecDbContext dbContext
) : INotificationHandler<MessageReceivedEvent>
{
    public async Task Handle(MessageReceivedEvent notification, CancellationToken cancellationToken)
    {
        var message = await dbContext.Set<ChannelMessage>()
            .Include(m => m.Author)
            .FirstOrDefaultAsync(m => m.Id == notification.Message.MessageId, cancellationToken);
            
        if (message != null)
        {
            await notificationService.ProcessMessageAsync(message, cancellationToken);
        }
    }
}
