using Fennec.App.Domain;
using Fennec.App.ViewModels;
using NodaTime;
using NodaTime.Text;

namespace Fennec.App.Formatting;

public class MessageItemBuilder
{
    public MessageItem Build(
        Guid messageId,
        string content,
        Guid authorId,
        string authorName,
        string? authorInstanceUrl,
        string createdAt,
        Instant? previousTimestamp,
        Guid? previousAuthorId)
    {
        var message = Message.Create(messageId, authorId, authorInstanceUrl, content, createdAt);
        var zone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        var showAuthor = MessageGrouper.ShouldShowAuthor(previousTimestamp, previousAuthorId, message.Timestamp, message.AuthorId, zone);
        var showTimeSeparator = MessageGrouper.ShouldShowTimeSeparator(previousTimestamp, message.Timestamp, zone);

        var display = new MessageDisplayModel(
            Content: content,
            AuthorId: authorId,
            AuthorName: authorName,
            AuthorInstanceUrl: authorInstanceUrl,
            AvatarFallback: authorName.Length > 0 ? authorName[..1].ToUpper() : "?",
            CreatedAt: createdAt,
            LocalTime: FormatLocalTime(createdAt),
            ExactTime: FormatExactTime(createdAt),
            ShowAuthor: showAuthor,
            ShowTimeSeparator: showTimeSeparator,
            TimeSeparatorText: showTimeSeparator ? FormatTimeSeparator(createdAt) : "");

        return new MessageItem
        {
            MessageId = messageId,
            Display = display,
        };
    }

    public static string FormatLocalTime(string instantString)
    {
        var result = InstantPattern.ExtendedIso.Parse(instantString);
        if (!result.Success) return "";

        var local = result.Value.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
        var now = SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());

        if (local.Date == now.Date)
            return local.ToString("HH:mm", null);

        if (local.Year == now.Year)
            return local.ToString("MMM dd, HH:mm", null);

        return local.ToString("yyyy MMM dd, HH:mm", null);
    }

    public static string FormatExactTime(string instantString)
    {
        var result = InstantPattern.ExtendedIso.Parse(instantString);
        if (!result.Success) return "";

        var local = result.Value.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
        return local.ToString("dddd, MMMM d, yyyy 'at' HH:mm:ss", null);
    }

    public static string FormatTimeSeparator(string instantString)
    {
        var result = InstantPattern.ExtendedIso.Parse(instantString);
        if (!result.Success) return "";

        var local = result.Value.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
        var now = SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());

        if (local.Date == now.Date)
            return "Today";

        if (local.Date == now.Date.PlusDays(-1))
            return "Yesterday";

        return local.ToString("MMMM d, yyyy", null);
    }
}
