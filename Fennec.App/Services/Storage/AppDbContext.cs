using Microsoft.EntityFrameworkCore;
using Fennec.App.Services.Storage.Models;

namespace Fennec.App.Services.Storage;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LocalServer> Servers => Set<LocalServer>();
    public DbSet<LocalUser> Users => Set<LocalUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LocalServer>(builder =>
        {
            builder.HasIndex(x => x.InstanceUrl);
            builder.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<LocalUser>(builder =>
        {
            builder.HasIndex(x => x.Username);
        });
    }
}
