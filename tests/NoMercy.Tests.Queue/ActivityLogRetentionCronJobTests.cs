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

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Queue.MediaServer.Jobs;
using Xunit;

namespace NoMercy.Tests.Queue;

public class ActivityLogRetentionCronJobTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];

    private IDbContextFactory<MediaContext> CreateFactory()
    {
        SqliteConnection connection = new(connectionString: "DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        _connections.Add(item: connection);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: connection)
            .Options;

        using (MediaContext init = new(options: options))
        {
            init.Database.EnsureCreated();
        }

        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(expression: x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: options));
        return mock.Object;
    }

    public void Dispose()
    {
        foreach (SqliteConnection connection in _connections)
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task Deletes_rows_older_than_retention_window()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid staleUser = Guid.NewGuid();
        Guid freshUser = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.ActivityLogs.AddRange(entities:
                [
                    new ActivityLog
                    {
                        Category = ActivityCategory.Connection,
                        Time = DateTime.UtcNow,
                        DeviceId = Ulid.NewUlid(),
                        UserId = staleUser,
                    },
                    new ActivityLog
                    {
                        Category = ActivityCategory.Connection,
                        Time = DateTime.UtcNow,
                        DeviceId = Ulid.NewUlid(),
                        UserId = freshUser,
                    }
                ]
            );
            await seed.SaveChangesAsync();

            // CreatedAt is stamped by SQLite's CURRENT_TIMESTAMP on insert, so the
            // retention-relevant timestamps are set explicitly via a direct UPDATE.
            await seed
                .ActivityLogs.Where(predicate: x => x.UserId == staleUser)
                .ExecuteUpdateAsync(setPropertyCalls: s =>
                    s.SetProperty(propertyExpression: x => x.CreatedAt, valueExpression: DateTime.UtcNow.AddDays(value: -31))
                );
            await seed
                .ActivityLogs.Where(predicate: x => x.UserId == freshUser)
                .ExecuteUpdateAsync(setPropertyCalls: s =>
                    s.SetProperty(propertyExpression: x => x.CreatedAt, valueExpression: DateTime.UtcNow.AddDays(value: -1))
                );
        }

        ActivityLogRetentionCronJob job = new(
            contextFactory: factory,
            logger: NullLogger<ActivityLogRetentionCronJob>.Instance,
            retentionDays: 30
        );
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ActivityLogs.Should().HaveCount(expected: 1);
        ctx.ActivityLogs.Single().CreatedAt.Should().BeAfter(expected: DateTime.UtcNow.AddDays(value: -30));
    }

    [Fact]
    public async Task Respects_configurable_retention_days()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid user = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.ActivityLogs.Add(
                entity: new()
                {
                    Category = ActivityCategory.Connection,
                    Time = DateTime.UtcNow,
                    DeviceId = Ulid.NewUlid(),
                    UserId = user,
                }
            );
            await seed.SaveChangesAsync();

            // CreatedAt is stamped by SQLite's CURRENT_TIMESTAMP on insert, so the
            // retention-relevant timestamp is set explicitly via a direct UPDATE.
            await seed
                .ActivityLogs.Where(predicate: x => x.UserId == user)
                .ExecuteUpdateAsync(setPropertyCalls: s =>
                    s.SetProperty(propertyExpression: x => x.CreatedAt, valueExpression: DateTime.UtcNow.AddDays(value: -8))
                );
        }

        ActivityLogRetentionCronJob job = new(
            contextFactory: factory,
            logger: NullLogger<ActivityLogRetentionCronJob>.Instance,
            retentionDays: 7
        );
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ActivityLogs.Should().BeEmpty();
    }

    [Fact]
    public void CronExpression_IsDailyAt3Am_AndJobNameIsSet()
    {
        ActivityLogRetentionCronJob job = new(
            contextFactory: CreateFactory(),
            logger: NullLogger<ActivityLogRetentionCronJob>.Instance
        );

        job.CronExpression.Should().Be(expected: "0 3 * * *");
        job.JobName.Should().Be(expected: "Activity Log Retention");
    }
}
