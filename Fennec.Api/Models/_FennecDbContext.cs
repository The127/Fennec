using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Models;

public class FennecDbContext : DbContext
{
    public FennecDbContext(DbContextOptions<FennecDbContext> options) 
        : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FennecDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
    }
}