using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public enum NotificationType
{
    Mention,
}

public class Notification : EntityBase
{
    public required Guid UserId { get; init; }
    public User User { get; init; } = null!;

    public required NotificationType Type { get; init; }

    public required Guid ServerId { get; init; }
    public required Guid ChannelId { get; init; }
    public required Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required string ContentExcerpt { get; init; }

    public bool IsRead { get; set; } = false;
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.AuthorName).HasMaxLength(100);
        builder.Property(x => x.ContentExcerpt).HasMaxLength(200);
    }
}
