using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.FederationApi;

[ApiController]
[Route("federation/v1/notification")]
[FederationAuth]
public class FederationNotificationController(
    FennecDbContext dbContext,
    INotificationEventService notificationEventService,
    IClockService clockService
) : FederationControllerBase
{
    [HttpPost("push")]
    public async Task<IActionResult> PushNotification([FromBody] PushNotificationRequestDto request, CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            UserId = request.TargetUserId,
            Type = request.Type,
            ServerId = request.ServerId,
            ChannelId = request.ChannelId,
            AuthorId = request.AuthorId,
            AuthorName = request.AuthorName,
            ContentExcerpt = request.ContentExcerpt,
            CreatedAt = clockService.GetCurrentInstant(),
            IsRead = false
        };

        dbContext.Set<Notification>().Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationEventService.NotifyNotificationReceived(request.TargetUserId, notification, cancellationToken);

        return Ok();
    }
}
