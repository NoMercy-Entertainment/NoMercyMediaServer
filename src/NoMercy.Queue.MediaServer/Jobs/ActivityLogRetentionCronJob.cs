using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Queue.MediaServer.Jobs;

public class ActivityLogRetentionCronJob : ICronJobExecutor
{
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly ILogger<ActivityLogRetentionCronJob> _logger;
    private readonly int _retentionDays;

    public string CronExpression => new CronExpressionBuilder().Daily(3);
    public string JobName => "Activity Log Retention";

    public ActivityLogRetentionCronJob(
        IDbContextFactory<MediaContext> contextFactory,
        ILogger<ActivityLogRetentionCronJob> logger,
        int retentionDays = 30
    )
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _retentionDays = retentionDays;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
        _logger.LogInformation(
            "Activity retention sweep starting; deleting rows older than {Cutoff}",
            cutoff
        );

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync(
            cancellationToken
        );
        List<Database.Models.Users.ActivityLog> stale = await ctx
            .ActivityLogs.Where(x => x.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        ctx.ActivityLogs.RemoveRange(stale);
        int deleted = await ctx.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Activity retention sweep complete; removed {Count} rows", deleted);
    }
}
