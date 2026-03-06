using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class ChannelGroup : EntityBase
{
    public required string Name { get; set; }
    
    public required Guid ServerId { get; init; }
    public Server Server { get; init; } = null!;

    public List<Channel> Channels { get; set; } = [];
}

public class ChannelGroupConfiguration : IEntityTypeConfiguration<ChannelGroup>
{
    public void Configure(EntityTypeBuilder<ChannelGroup> builder)
    {
    }
}