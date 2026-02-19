using Microsoft.EntityFrameworkCore;
using NoMercyQueue.Sqlite.Entities;

namespace NoMercyQueue.Sqlite;

internal class QueueDbContext : DbContext
{
    public QueueDbContext(DbContextOptions<QueueDbContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
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

        modelBuilder.Entity<QueueJobEntity>()
            .Property(j => j.Payload)
            .HasMaxLength(4096);

        base.OnModelCreating(modelBuilder);
    }

    public DbSet<QueueJobEntity> QueueJobs { get; set; }
    public DbSet<FailedJobEntity> FailedJobs { get; set; }
    public DbSet<CronJobEntity> CronJobs { get; set; }
}
