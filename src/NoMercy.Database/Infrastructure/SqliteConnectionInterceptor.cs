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

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NoMercy.Database;

/// <summary>
/// Configures each SQLite connection for concurrent use:
/// - WAL journal mode: allows concurrent readers alongside the writer
/// - busy_timeout: retry for up to 5 s instead of immediately throwing SQLITE_BUSY
/// - synchronous=NORMAL: safe with WAL and faster than FULL
/// </summary>
public class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        ApplyPragmas(connection);
        await Task.CompletedTask;
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using DbCommand cmd = connection.CreateCommand();

        // busy_timeout and synchronous are connection-level — always safe to set.
        // Kept short deliberately: SqliteRetryingExecutionStrategy already retries
        // "database is locked" with its own exponential backoff on top of whatever
        // this pragma blocks for per attempt. Stacking a long busy_timeout under a
        // multi-retry backoff compounds — a single contended query can silently
        // stall for (busy_timeout + backoff) x retry-count, tens of seconds past
        // what any interactive caller (e.g. a SignalR hub method a client is
        // waiting on) should ever wait. 5 s absorbs a normal contention blip;
        // anything longer should surface to the execution strategy's own bounded
        // retry loop instead of being invisible inside the driver.
        cmd.CommandText = """
            PRAGMA busy_timeout=5000;
            PRAGMA synchronous=NORMAL;
            """;
        cmd.ExecuteNonQuery();

        // journal_mode=WAL writes to the database file — may fail on read-only
        // or not-yet-initialized databases. DatabaseSeeder.Migrate() applies it
        // explicitly after schema setup, so this is best-effort.
        try
        {
            using DbCommand walCmd = connection.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Silently skip — WAL will be set after migration completes.
        }
    }
}
