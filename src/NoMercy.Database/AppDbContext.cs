using Microsoft.EntityFrameworkCore;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Information;

namespace NoMercy.Database;

public class AppDbContext : DbContext
{
    public DbSet<Configuration> Configuration { get; set; }

    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.Name is "CreatedAt" or "UpdatedAt")
            .ToList()
            .ForEach(p => p.SetDefaultValueSql("CURRENT_TIMESTAMP"));

        modelBuilder
            .Entity<Configuration>()
            .Property(e => e.SecureValue)
            .HasConversion(v => TokenStore.EncryptToken(v), v => TokenStore.DecryptToken(v));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={AppFiles.AppDatabase}; Foreign Keys=True;");
        }
    }
}
