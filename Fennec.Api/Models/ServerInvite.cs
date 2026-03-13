using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Fennec.Api.Models;

public class ServerInvite : EntityBase
{
    public required Guid ServerId { get; init; }
    public Server Server { get; init; } = null!;

    public required string Code { get; set; }

    public required Guid CreatedByKnownUserId { get; init; }
    public KnownUser CreatedByKnownUser { get; init; } = null!;

    public Instant? ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public int Uses { get; set; }
}

public class ServerInviteConfiguration : IEntityTypeConfiguration<ServerInvite>
{
    public void Configure(EntityTypeBuilder<ServerInvite> builder)
    {
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.ServerId);
    }
}
