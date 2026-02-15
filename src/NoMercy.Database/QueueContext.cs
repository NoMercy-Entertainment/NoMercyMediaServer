using Microsoft.EntityFrameworkCore;
using NoMercy.NmSystem.Information;

namespace NoMercy.Database;

public class QueueContext : DbContext
{
    public QueueContext(DbContextOptions<QueueContext> options) : base(options)
    {
    }

    public QueueContext()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={AppFiles.QueueDatabase}; Pooling=True; Cache=Shared; Foreign Keys=True;");
        }

    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<string>()
            .HaveMaxLength(256);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.Name is "CreatedAt" or "UpdatedAt")
            .ToList()
            .ForEach(p => p.SetDefaultValueSql("CURRENT_TIMESTAMP"));

        modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .ToList()
            .ForEach(p => p.DeleteBehavior = DeleteBehavior.Cascade);

        modelBuilder.Entity<QueueJob>()
            .Property(j => j.Payload)
            .HasMaxLength(4096);

        base.OnModelCreating(modelBuilder);
    }

    public virtual DbSet<QueueJob> QueueJobs { get; set; }
    public virtual DbSet<FailedJob> FailedJobs { get; set; }
    public virtual DbSet<CronJob> CronJobs { get; set; }
}