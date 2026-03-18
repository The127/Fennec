using NodaTime;
using NodaTime.Text;

namespace Fennec.App.Domain;

public readonly record struct Message(Guid Id, Guid AuthorId, string? AuthorInstanceUrl, string Content, Instant Timestamp)
{
    public static Message Create(Guid id, Guid authorId, string? authorInstanceUrl, string content, string isoTimestamp)
    {
        var result = InstantPattern.ExtendedIso.Parse(isoTimestamp);
        if (!result.Success)
            throw new ArgumentException($"Unparseable ISO timestamp: '{isoTimestamp}'", nameof(isoTimestamp));
        return new Message(id, authorId, authorInstanceUrl, content, result.Value);
    }
}
