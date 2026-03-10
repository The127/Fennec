using Fennec.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class KnownServer : EntityBase
{
    public required Guid RemoteId { get; init; }
    public required string InstanceUrl { get; init; }
}

public class RemoteServerConfiguration : IEntityTypeConfiguration<KnownServer>
{
    public void Configure(EntityTypeBuilder<KnownServer> builder)
    {
        builder.HasIndex(x => new { x.RemoteId, x.InstanceUrl }).IsUnique();
    }
}