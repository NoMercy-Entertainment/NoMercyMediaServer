// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

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

    // Bounded deliberately low: this is one of TWO independent wait layers a
    // contended query passes through — Microsoft.Data.Sqlite's own busy_timeout
    // pragma (see SqliteConnectionInterceptor) already absorbs short contention
    // blips inside the driver before an exception ever reaches here. Stacking a
    // 6-retry, up-to-30s-per-retry backoff ON TOP of that turned a live incident
    // into a 79s stall on an interactive SignalR hub method a client was blocking
    // on (see MusicHub.StartPlaybackCommand) — worse, one long enough to trip an
    // unrelated 15s liveness watchdog and kill the user's session as a side effect.
    // 4 retries up to 5s absorbs a real contention burst (sum of backoff delays
    // ~9s) without manufacturing a minute-plus hang out of transient lock
    // contention that WAL-mode SQLite should clear in low single digits of
    // seconds under normal load.
    private new const int DefaultMaxRetryCount = 4;
    private static new readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(5);

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
                // Contention was previously invisible end to end — a stalled query
                // just looked slow, with nothing in the log distinguishing "the
                // query is slow" from "the query is retrying behind another
                // writer". This makes the next incident provable at a glance.
                Logger.System(
                    $"SQLite lock contention — retry {ExceptionsEncountered.Count} of {DefaultMaxRetryCount}: {current.Message}",
                    LogEventLevel.Warning
                );
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether an exception (or any in its chain) is a transient SQLite lock error.
    /// Useful for callers outside EF Core's pipeline (FlexLabs.Upsert, raw SQL, SignalR hubs).
    /// </summary>
    public static bool IsTransientSqliteError(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
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

    /// <summary>
    /// Executes an async operation with retry on transient SQLite lock errors.
    /// Use this to wrap calls that bypass the EF Core execution strategy
    /// (e.g. FlexLabs.Upsert, ExecuteSqlRaw, SignalR hub methods).
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = DefaultMaxRetryCount,
        CancellationToken cancellationToken = default
    )
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (IsTransientSqliteError(ex) && attempt < maxRetries)
            {
                int delay = Math.Min(
                    (int)(Math.Pow(2, attempt) * 1000 * (1.0 + Random.Shared.NextDouble() * 0.1)),
                    (int)DefaultMaxDelay.TotalMilliseconds
                );

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <inheritdoc cref="ExecuteWithRetryAsync(Func{Task}, int, CancellationToken)"/>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = DefaultMaxRetryCount,
        CancellationToken cancellationToken = default
    )
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransientSqliteError(ex) && attempt < maxRetries)
            {
                int delay = Math.Min(
                    (int)(Math.Pow(2, attempt) * 1000 * (1.0 + Random.Shared.NextDouble() * 0.1)),
                    (int)DefaultMaxDelay.TotalMilliseconds
                );

                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
