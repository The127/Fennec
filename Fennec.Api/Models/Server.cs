using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class Server : EntityBase
{
    public required string Name { get; set; }
    public required string Slug { get; init; }
    
    public List<ServerMember> Members { get; set; } = new();
}

public class ServerConfiguration : IEntityTypeConfiguration<Server>
{
    public void Configure(EntityTypeBuilder<Server> builder)
    {
        builder.HasIndex(x => x.Name);
        
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}