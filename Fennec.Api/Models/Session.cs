using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class Session : EntityBase
{
    public required Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public required string Token { get; set; }
}

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
    }
}