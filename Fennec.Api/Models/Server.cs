using Fennec.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class Server : EntityBase
{
    public required string Name { get; set; }
    public required ServerVisibility Visibility { get; set; }
    
    public List<ServerMember> Members { get; set; } = [];
    public List<ChannelGroup> ChannelGroups { get; set; } = [];
    public List<Channel> Channels { get; set; } = [];
}

public class ServerConfiguration : IEntityTypeConfiguration<Server>
{
    public void Configure(EntityTypeBuilder<Server> builder)
    {
        builder.HasIndex(x => x.Name);
    }
}