using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class ServerMember : EntityBase
{
    public required Guid ServerId { get; init; }
    public Server Server { get; init; } = null!;
    
    public required Guid UserId { get; init; }
    public User User { get; init; } = null!;
}

public class ServerMemberConfiguration : IEntityTypeConfiguration<ServerMember>
{
    public void Configure(EntityTypeBuilder<ServerMember> builder)
    {
    }
}