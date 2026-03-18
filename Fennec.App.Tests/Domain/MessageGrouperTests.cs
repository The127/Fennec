using Fennec.App.Domain;
using NodaTime;

namespace Fennec.App.Tests.Domain;

public class MessageGrouperTests
{
    private static Instant At(int hour, int minute = 0, int day = 1) =>
        Instant.FromUtc(2024, 1, day, hour, minute);

    private static readonly Guid AuthorA = Guid.NewGuid();
    private static readonly Guid AuthorB = Guid.NewGuid();

    private static readonly DateTimeZone Zone = DateTimeZone.Utc;

    [Fact]
    public void Same_author_within_5_minutes_no_header()
    {
        var prev = At(12, 0);
        var curr = At(12, 4);
        Assert.False(MessageGrouper.ShouldShowAuthor(prev, AuthorA, curr, AuthorA, Zone));
    }

    [Fact]
    public void Same_author_after_5_minutes_shows_header()
    {
        var prev = At(12, 0);
        var curr = At(12, 5);
        Assert.True(MessageGrouper.ShouldShowAuthor(prev, AuthorA, curr, AuthorA, Zone));
    }

    [Fact]
    public void Different_author_same_minute_shows_header()
    {
        var prev = At(12, 0);
        var curr = At(12, 0);
        Assert.True(MessageGrouper.ShouldShowAuthor(prev, AuthorA, curr, AuthorB, Zone));
    }

    [Fact]
    public void New_day_shows_header()
    {
        var prev = At(12, 0, day: 1);
        var curr = At(12, 0, day: 2);
        Assert.True(MessageGrouper.ShouldShowAuthor(prev, AuthorA, curr, AuthorA, Zone));
    }

    [Fact]
    public void First_message_previous_null_shows_header_no_separator()
    {
        Assert.True(MessageGrouper.ShouldShowAuthor(null, null, At(12, 0), AuthorA, Zone));
        Assert.False(MessageGrouper.ShouldShowTimeSeparator(null, At(12, 0), Zone));
    }

    [Fact]
    public void New_day_shows_separator()
    {
        var prev = At(12, 0, day: 1);
        var curr = At(12, 0, day: 2);
        Assert.True(MessageGrouper.ShouldShowTimeSeparator(prev, curr, Zone));
    }

    [Fact]
    public void Same_day_no_separator()
    {
        var prev = At(9, 0);
        var curr = At(17, 0);
        Assert.False(MessageGrouper.ShouldShowTimeSeparator(prev, curr, Zone));
    }
}
