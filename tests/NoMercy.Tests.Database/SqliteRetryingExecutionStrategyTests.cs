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

using System.Reflection;
using NoMercy.Database;

namespace NoMercy.Tests.Database;

/// <summary>
/// Covers the live-incident fix bounding <see cref="SqliteRetryingExecutionStrategy"/>'s retry
/// budget: the previous 6-retry/30s-max-delay ceiling stacked with the connection-level
/// busy_timeout pragma turned a real SQLite lock-contention burst into a 79-second stall on an
/// interactive SignalR hub method (MusicHub.StartPlaybackCommand's GetPlaylist), which then
/// tripped an unrelated 15s liveness watchdog and killed the user's session as a side effect.
/// The budget must now be small enough that a genuine contention burst resolves in low
/// single-digit seconds of backoff, not tens of seconds.
/// </summary>
public class SqliteRetryingExecutionStrategyTests
{
    // Named exactly "SqliteException" — ShouldRetryOn/IsTransientSqliteError match by
    // GetType().Name to avoid a hard reference to Microsoft.Data.Sqlite, so a type with this
    // exact simple name (regardless of namespace) exercises the real matching logic without
    // needing to construct the real ADO exception type.
    private sealed class SqliteException(string message) : Exception(message);

    [Fact]
    public void DefaultMaxRetryCount_IsBoundedToFour()
    {
        FieldInfo? field = typeof(SqliteRetryingExecutionStrategy).GetField(
            "DefaultMaxRetryCount",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.NotNull(field);
        Assert.Equal(4, (int)field!.GetRawConstantValue()!);
    }

    [Fact]
    public void DefaultMaxDelay_IsBoundedToFiveSeconds()
    {
        FieldInfo? field = typeof(SqliteRetryingExecutionStrategy).GetField(
            "DefaultMaxDelay",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.NotNull(field);
        TimeSpan value = (TimeSpan)field!.GetValue(null)!;
        Assert.Equal(TimeSpan.FromSeconds(5), value);
    }

    [Fact]
    public void IsTransientSqliteError_True_ForLockedMessage()
    {
        Exception ex = new SqliteException("database is locked");

        Assert.True(SqliteRetryingExecutionStrategy.IsTransientSqliteError(ex));
    }

    [Fact]
    public void IsTransientSqliteError_True_WhenLockedExceptionIsNested()
    {
        Exception ex = new InvalidOperationException(
            "wrapper",
            new SqliteException("database table is locked")
        );

        Assert.True(SqliteRetryingExecutionStrategy.IsTransientSqliteError(ex));
    }

    [Fact]
    public void IsTransientSqliteError_False_ForUnrelatedException()
    {
        Exception ex = new InvalidOperationException("not a lock error");

        Assert.False(SqliteRetryingExecutionStrategy.IsTransientSqliteError(ex));
    }

    [Fact]
    public void IsTransientSqliteError_False_ForSqliteExceptionWithUnrelatedMessage()
    {
        Exception ex = new SqliteException("no such table: Foo");

        Assert.False(SqliteRetryingExecutionStrategy.IsTransientSqliteError(ex));
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_StopsAfterMaxRetries_AndSurfacesTheException()
    {
        int attempts = 0;

        async Task Operation()
        {
            attempts++;
            await Task.CompletedTask;
            throw new SqliteException("database is locked");
        }

        // maxRetries: 1 keeps this test fast (one real backoff sleep) while still proving
        // the loop honors an explicit bound rather than retrying forever.
        await Assert.ThrowsAsync<SqliteException>(() =>
            SqliteRetryingExecutionStrategy.ExecuteWithRetryAsync(Operation, maxRetries: 1)
        );

        Assert.Equal(2, attempts); // initial attempt + 1 retry
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_SucceedsOnceTheOperationStopsThrowing()
    {
        int attempts = 0;

        async Task<int> Operation()
        {
            attempts++;
            await Task.CompletedTask;
            if (attempts < 2)
                throw new SqliteException("database is locked");
            return 42;
        }

        int result = await SqliteRetryingExecutionStrategy.ExecuteWithRetryAsync(
            Operation,
            maxRetries: 3
        );

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_DoesNotRetry_NonTransientException()
    {
        int attempts = 0;

        async Task Operation()
        {
            attempts++;
            await Task.CompletedTask;
            throw new InvalidOperationException("not transient");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SqliteRetryingExecutionStrategy.ExecuteWithRetryAsync(Operation, maxRetries: 3)
        );

        Assert.Equal(1, attempts);
    }
}
