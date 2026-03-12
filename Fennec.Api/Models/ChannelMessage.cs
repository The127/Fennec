using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public enum MessageType
{
    Text,
}

public class TextMessage
{
    public string Content { get; set; } = "";
}

public class ChannelMessage : EntityBase
{
    public required Guid ChannelId { get; init; }
    public Channel Channel { get; init; } = null!;

    public required Guid AuthorId { get; init; }
    public User Author { get; init; } = null!;

    public required MessageType Type { get; set; }
    public required JsonDocument Details { get; set; }
}

public class ChannelMessageConfiguration : IEntityTypeConfiguration<ChannelMessage>
{
    public void Configure(EntityTypeBuilder<ChannelMessage> builder)
    {
    }
}
