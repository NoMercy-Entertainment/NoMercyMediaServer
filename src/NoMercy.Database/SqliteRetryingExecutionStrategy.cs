using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace NoMercy.Database;

/// <summary>
/// Custom execution strategy that retries EF Core operations on transient SQLite
/// lock contention errors:
///   - SQLITE_BUSY   (Error 5)  — "database is locked"
///   - SQLITE_LOCKED (Error 6)  — "database table is locked" (shared-cache / trigger contention)
///
/// This protects ALL database consumers (API controllers, SignalR hubs, background workers)
/// without requiring per-call retry logic.
/// </summary>
public class SqliteRetryingExecutionStrategy : ExecutionStrategy
{
    public SqliteRetryingExecutionStrategy(DbContext context)
        : base(context, DefaultMaxRetryCount, DefaultMaxDelay) { }

    public SqliteRetryingExecutionStrategy(
        DbContext context,
        int maxRetryCount,
        TimeSpan maxRetryDelay
    )
        : base(context, maxRetryCount, maxRetryDelay) { }

    public SqliteRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, DefaultMaxRetryCount, DefaultMaxDelay) { }

    public SqliteRetryingExecutionStrategy(
        ExecutionStrategyDependencies dependencies,
        int maxRetryCount,
        TimeSpan maxRetryDelay
    )
        : base(dependencies, maxRetryCount, maxRetryDelay) { }

    private new const int DefaultMaxRetryCount = 6;
    private static new readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The base <see cref="ExecutionStrategy"/> rejects any ambient transaction on first
    /// execution because retrying a partial transaction is generally unsafe. EF Core's
    /// split queries open an internal transaction for consistency, which triggers this
    /// check. For SQLite lock contention the retry is safe — the lock error fires before
    /// any rows are modified — so we skip the transaction guard and just clear state.
    /// </summary>
    protected override void OnFirstExecution()
    {
        // Intentionally skip base.OnFirstExecution() to avoid the transaction check.
        // Only clear the exception list so retry counting starts fresh.
        ExceptionsEncountered.Clear();
    }

    protected override bool ShouldRetryOn(Exception exception)
    {
        // Walk the full exception chain
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            // Check by type name to avoid a hard reference to Microsoft.Data.Sqlite
            if (
                current.GetType().Name == "SqliteException"
                && current.Message.Contains("is locked", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }
}
