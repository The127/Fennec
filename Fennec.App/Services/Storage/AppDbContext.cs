using Microsoft.EntityFrameworkCore;
using Fennec.App.Services.Storage.Models;

namespace Fennec.App.Services.Storage;

public class AppDbContext(DbContextOptions<AppDbContext> options, IDbPathProvider? dbPathProvider = null) : DbContext(options)
{
    public DbSet<LocalServer> Servers => Set<LocalServer>();
    public DbSet<LocalUser> Users => Set<LocalUser>();
    public DbSet<LocalChannelGroup> ChannelGroups => Set<LocalChannelGroup>();
    public DbSet<LocalChannel> Channels => Set<LocalChannel>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && dbPathProvider?.CurrentDbPath != null)
        {
            optionsBuilder.UseSqlite($"Data Source={dbPathProvider.CurrentDbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LocalServer>(builder =>
        {
            builder.HasIndex(x => x.InstanceUrl);
            builder.HasIndex(x => x.SortOrder);
            builder.HasMany(x => x.ChannelGroups)
                .WithOne()
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocalChannelGroup>(builder =>
        {
            builder.HasIndex(x => x.ServerId);
            builder.HasIndex(x => x.SortOrder);
            builder.HasMany(x => x.Channels)
                .WithOne()
                .HasForeignKey(x => x.ChannelGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocalChannel>(builder =>
        {
            builder.HasIndex(x => x.ChannelGroupId);
            builder.HasIndex(x => x.ServerId);
            builder.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<LocalUser>(builder =>
        {
            builder.HasIndex(x => x.Username);
        });
    }
}
