using Microsoft.EntityFrameworkCore;
using NoMercy.Database.Models;
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
        options.UseSqlite($"Data Source={AppFiles.QueueDatabase}; Pooling=True; Cache=Shared; Foreign Keys=True;");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<string>()
            .HaveMaxLength(256);

        // Configure Ulid to string conversion for EncoderV2 entities
        configurationBuilder
            .Properties<Ulid>()
            .HaveConversion<UlidToStringConverter>();
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

        base.OnModelCreating(modelBuilder);
    }

    public virtual DbSet<QueueJob> QueueJobs { get; set; }
    public virtual DbSet<FailedJob> FailedJobs { get; set; }
    public virtual DbSet<CronJob> CronJobs { get; set; }

    // EncoderV2 entities
    public virtual DbSet<EncodingJob> EncodingJobs { get; set; }
    public virtual DbSet<EncodingTask> EncodingTasks { get; set; }
    public virtual DbSet<EncoderNode> EncoderNodes { get; set; }
    public virtual DbSet<EncodingProgress> EncodingProgress { get; set; }
}