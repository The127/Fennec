using NodaTime;

namespace Fennec.App.Domain;

public static class MessageGrouper
{
    public static bool ShouldShowAuthor(Instant? previous, Guid? previousAuthorId, Instant? current, Guid currentAuthorId)
    {
        if (previous is null || current is null) return true;
        if (currentAuthorId != previousAuthorId) return true;
        if ((current.Value - previous.Value).TotalMinutes >= 5) return true;
        return ShouldShowTimeSeparator(previous, current);
    }

    public static bool ShouldShowTimeSeparator(Instant? previous, Instant? current)
    {
        if (previous is null || current is null) return false;
        var zone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        var prevDate = previous.Value.InZone(zone).Date;
        var currDate = current.Value.InZone(zone).Date;
        return currDate != prevDate;
    }
}
