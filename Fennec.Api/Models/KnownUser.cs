using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class KnownUser : EntityBase
{
    public required Guid RemoteId { get; init; }
    public required string? InstanceUrl { get; init; }
    
    public required string Name { get; set; }
    
    public List<ServerMember> ServerMembers { get; set; } = [];
}

public class KnownUserConfiguration : IEntityTypeConfiguration<KnownUser>
{
    public void Configure(EntityTypeBuilder<KnownUser> builder)
    {
        builder.HasIndex(x => new { x.RemoteId, x.InstanceUrl }).IsUnique();
    }
}
