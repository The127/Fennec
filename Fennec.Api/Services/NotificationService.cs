using System.Text.Json;
using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.FederationClient;
using Fennec.Api.Models;
using Fennec.Api.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fennec.Api.Services;

public interface INotificationService
{
    Task ProcessMessageAsync(ChannelMessage message, CancellationToken cancellationToken);
}

public class NotificationService(
    FennecDbContext dbContext,
    IMentionParser mentionParser,
    IFederationClient federationClient,
    IOptions<FennecSettings> fennecSettings,
    INotificationEventService notificationEventService,
    IClockService clockService
) : INotificationService
{
    public async Task ProcessMessageAsync(ChannelMessage message, CancellationToken cancellationToken)
    {
        if (message.Type != MessageType.Text) return;
        
        var textMessage = message.Details.Deserialize<TextMessage>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (textMessage == null || string.IsNullOrWhiteSpace(textMessage.Content)) return;

        var mentions = mentionParser.ParseMentions(textMessage.Content).ToList();
        if (mentions.Count == 0) return;

        // Find members of this server that match the mentions
        // We need ServerId, but ChannelMessage only has ChannelId. We need to join with Channel.
        var channel = await dbContext.Set<Channel>().SingleAsync(c => c.Id == message.ChannelId, cancellationToken);
        var mentionedMembers = await dbContext.Set<ServerMember>()
            .Include(m => m.KnownUser)
            .Where(m => m.ServerId == channel.ServerId && mentions.Contains(m.KnownUser.Name))
            .Where(m => !m.KnownUser.IsDeleted)
            .ToListAsync(cancellationToken);

        var myInstanceUrl = fennecSettings.Value.IssuerUrl;

        foreach (var member in mentionedMembers)
        {
            var targetInstanceUrl = member.KnownUser.InstanceUrl ?? myInstanceUrl;
            
            var request = new PushNotificationRequestDto
            {
                TargetUserId = member.KnownUser.RemoteId,
                Type = NotificationType.Mention,
                ServerId = member.ServerId,
                ChannelId = message.ChannelId,
                AuthorId = message.AuthorId,
                AuthorName = message.Author.Name,
                ContentExcerpt = textMessage.Content.Length > 100 ? textMessage.Content[..100] + "..." : textMessage.Content
            };

            if (targetInstanceUrl == myInstanceUrl)
            {
                // Local notification delivery
                await DeliverLocalNotification(request, cancellationToken);
            }
            else
            {
                // Remote notification delivery via federation
                try
                {
                    await federationClient.For(targetInstanceUrl).Notification.PushNotificationAsync(request, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log and ignore for now (could implement retry logic later)
                    Console.WriteLine($"[DEBUG_LOG] Failed to push notification to {targetInstanceUrl}: {ex.Message}");
                }
            }
        }
    }

    private async Task DeliverLocalNotification(PushNotificationRequestDto request, CancellationToken cancellationToken)
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
    }
}
