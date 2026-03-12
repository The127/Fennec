using Fennec.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class Channel : EntityBase
{
    public required string Name { get; set; }

    public required Guid ServerId { get; init; }
    public Server Server { get; init; } = null!;

    public required Guid ChannelGroupId { get; set; }
    public ChannelGroup ChannelGroup { get; set; } = null!;

    public ChannelType ChannelType { get; set; } = ChannelType.TextAndVoice;

    public List<ChannelMessage> Messages { get; set; } = [];
}

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
    }
}