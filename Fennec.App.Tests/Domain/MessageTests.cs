using Fennec.App.Domain;
using NodaTime;
using NodaTime.Text;

namespace Fennec.App.Tests.Domain;

public class MessageTests
{
    private static readonly Guid SomeId = Guid.NewGuid();
    private static readonly Guid SomeAuthorId = Guid.NewGuid();
    private const string ValidIso = "2024-06-15T10:30:00Z";

    [Fact]
    public void Valid_inputs_produce_correct_properties()
    {
        var msg = Message.Create(SomeId, SomeAuthorId, "remote.example.com", "hello world", ValidIso);

        Assert.Equal(SomeId, msg.Id);
        Assert.Equal(SomeAuthorId, msg.AuthorId);
        Assert.Equal("remote.example.com", msg.AuthorInstanceUrl);
        Assert.Equal("hello world", msg.Content);
        Assert.Equal(InstantPattern.ExtendedIso.Parse(ValidIso).Value, msg.Timestamp);
    }

    [Fact]
    public void Null_author_instance_url_is_stored_as_null()
    {
        var msg = Message.Create(SomeId, SomeAuthorId, null, "hello", ValidIso);
        Assert.Null(msg.AuthorInstanceUrl);
    }

    [Fact]
    public void Unparseable_timestamp_throws_argument_exception()
    {
        Assert.Throws<ArgumentException>(() =>
            Message.Create(SomeId, SomeAuthorId, null, "hello", "not-a-timestamp"));
    }

    [Fact]
    public void Content_is_stored_as_is_without_trimming()
    {
        var msg = Message.Create(SomeId, SomeAuthorId, null, "  spaced  ", ValidIso);
        Assert.Equal("  spaced  ", msg.Content);
    }
}
