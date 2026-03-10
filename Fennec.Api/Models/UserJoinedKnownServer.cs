using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class UserJoinedKnownServer : EntityBase
{
    public required Guid UserId { get; init; }
    public User User { get; init; } = null!;
    
    public required Guid KnownServerId { get; init; }
    public KnownServer KnownServer { get; init; } = null!;
}

public class UserJoinedKnownServerConfiguration : IEntityTypeConfiguration<UserJoinedKnownServer>
{
    public void Configure(EntityTypeBuilder<UserJoinedKnownServer> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.KnownServerId }).IsUnique();
    }
}