using System.Text.Json;
using System.Text.Json.Serialization;
using EntityFrameworkCore.Projectables;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fennec.Api.Models;

public enum AuthMethodType
{
    Password,
}

public class PasswordAuthMethodDetails
{
    public required string Hash { get; set; }
}

public class AuthMethod : EntityBase
{
    public required Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required AuthMethodType Type { get; set; }

    public required JsonDocument Details { get; set; }
}

public class AuthMethodConfiguration : IEntityTypeConfiguration<AuthMethod>
{
    public void Configure(EntityTypeBuilder<AuthMethod> builder)
    {
    }
}

public class PasswordAuthMethod : EntityBase
{
    public required Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public required PasswordAuthMethodDetails Details { get; set; }
}

public static class AuthMethodExtensions
{
    [Projectable]
    public static IQueryable<PasswordAuthMethod> WherePasswordAuthMethod(this IQueryable<AuthMethod> query) => query
        .Where(x => x.Type == AuthMethodType.Password)
        .Select(x => new PasswordAuthMethod
        {
            UserId = x.UserId,
            Details = new PasswordAuthMethodDetails
            {
                Hash = x.Details.RootElement.GetProperty(nameof(PasswordAuthMethodDetails.Hash)).GetString()!,
            },
        });
}
