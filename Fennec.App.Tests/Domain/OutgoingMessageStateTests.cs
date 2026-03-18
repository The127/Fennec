using Fennec.App.Domain;
using Fennec.App.ViewModels;

namespace Fennec.App.Tests.Domain;

public class OutgoingMessageStateTests
{
    private static MessageItem NewMessage() => new()
    {
        Display = new MessageDisplayModel(
            Content: "hello",
            AuthorId: Guid.NewGuid(),
            AuthorName: "alice",
            AuthorInstanceUrl: null,
            AvatarFallback: "A",
            CreatedAt: "2024-01-01T12:00:00Z",
            LocalTime: "12:00",
            ExactTime: "Monday, January 1, 2024 at 12:00:00",
            ShowAuthor: true,
            ShowTimeSeparator: false,
            TimeSeparatorText: ""),
    };

    [Fact]
    public void New_MessageItem_has_null_SendState()
    {
        Assert.Null(NewMessage().SendState);
    }

    [Fact]
    public void PendingState_makes_IsPending_true_and_IsSendFailed_false()
    {
        var item = NewMessage();
        item.SendState = new PendingState();
        Assert.True(item.IsPending);
        Assert.False(item.IsSendFailed);
    }

    [Fact]
    public void DeliveredState_makes_both_false()
    {
        var item = NewMessage();
        item.SendState = new DeliveredState();
        Assert.False(item.IsPending);
        Assert.False(item.IsSendFailed);
    }

    [Fact]
    public void FailedState_makes_IsSendFailed_true_and_IsPending_false()
    {
        var item = NewMessage();
        item.SendState = new FailedState("Send failed");
        Assert.False(item.IsPending);
        Assert.True(item.IsSendFailed);
    }

    [Fact]
    public void FailedState_carries_Reason_string()
    {
        var state = new FailedState("Network error");
        Assert.Equal("Network error", state.Reason);
    }
}
