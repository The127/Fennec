using Fennec.App.Helpers;
using AppInstanceUrl = Fennec.App.Domain.InstanceUrl;

namespace Fennec.App.Domain;

public record MessageDisplayModel(
    string Content,
    Guid AuthorId,
    string AuthorName,
    string? AuthorInstanceUrl,
    string AvatarFallback,
    string CreatedAt,
    string LocalTime,
    string ExactTime,
    bool ShowAuthor,
    bool ShowTimeSeparator,
    string TimeSeparatorText)
{
    public string AuthorIdentity => new FederatedAddress(AuthorName, AppInstanceUrl.From(AuthorInstanceUrl)).ToString();
    public bool IsEmojiOnly => !string.IsNullOrWhiteSpace(Content) && EmojiHelper.IsAllEmoji(Content);
}
