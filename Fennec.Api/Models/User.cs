using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public class User : EntityBase
{
    public required string Name { get; set; }
    
    public required bool IsLocal { get; set; }
    
    public List<AuthMethod> AuthMethods { get; init; } = new();
    public List<Session> Sessions { get; init; } = new();
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
