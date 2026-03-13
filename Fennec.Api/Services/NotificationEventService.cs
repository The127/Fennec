using Fennec.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Fennec.Api.Hubs;

namespace Fennec.Api.Services;

public interface INotificationEventService
{
    Task NotifyNotificationReceived(Guid userId, Notification notification, CancellationToken cancellationToken);
}

public class NotificationEventService(
    IHubContext<MessageHub> messageHubContext
) : INotificationEventService
{
    public Task NotifyNotificationReceived(Guid userId, Notification notification, CancellationToken cancellationToken)
    {
        // We use the User ID as the group name for personal notifications
        return messageHubContext.Clients.User(userId.ToString())
            .SendAsync("NotificationReceived", notification, cancellationToken: cancellationToken);
    }
}
