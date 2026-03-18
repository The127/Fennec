using Fennec.App.Domain;

namespace Fennec.App.Tests.Domain;

public class MessageLengthPolicyTests
{
    [Fact]
    public void Empty_string_not_over_limit()
    {
        Assert.False(MessageLengthPolicy.IsOverLimit(""));
    }

    [Fact]
    public void Exactly_MaxLength_not_over_limit()
    {
        var text = new string('a', MessageLengthPolicy.MaxLength);
        Assert.False(MessageLengthPolicy.IsOverLimit(text));
    }

    [Fact]
    public void MaxLength_plus_one_is_over_limit()
    {
        var text = new string('a', MessageLengthPolicy.MaxLength + 1);
        Assert.True(MessageLengthPolicy.IsOverLimit(text));
    }

    [Fact]
    public void Below_CounterVisibleThreshold_does_not_show_counter()
    {
        var text = new string('a', MessageLengthPolicy.CounterVisibleThreshold - 1);
        Assert.False(MessageLengthPolicy.ShouldShowCounter(text));
    }

    [Fact]
    public void At_CounterVisibleThreshold_shows_counter()
    {
        var text = new string('a', MessageLengthPolicy.CounterVisibleThreshold);
        Assert.True(MessageLengthPolicy.ShouldShowCounter(text));
    }

    [Fact]
    public void CharsRemaining_returns_correct_value()
    {
        Assert.Equal(MessageLengthPolicy.MaxLength - 42, MessageLengthPolicy.CharsRemaining(new string('a', 42)));
    }
}
