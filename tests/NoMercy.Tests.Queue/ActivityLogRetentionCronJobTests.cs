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
        SqliteConnection connection = new("DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        _connections.Add(connection);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection)
            .Options;

        using (MediaContext init = new(options))
        {
            init.Database.EnsureCreated();
        }

        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new(options));
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
            seed.ActivityLogs.AddRange([
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
                .ActivityLogs.Where(x => x.UserId == staleUser)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.CreatedAt, DateTime.UtcNow.AddDays(-31))
                );
            await seed
                .ActivityLogs.Where(x => x.UserId == freshUser)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.CreatedAt, DateTime.UtcNow.AddDays(-1))
                );
        }

        ActivityLogRetentionCronJob job = new(
            factory,
            NullLogger<ActivityLogRetentionCronJob>.Instance,
            30
        );
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ActivityLogs.Should().HaveCount(1);
        ctx.ActivityLogs.Single().CreatedAt.Should().BeAfter(DateTime.UtcNow.AddDays(-30));
    }

    [Fact]
    public async Task Respects_configurable_retention_days()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid user = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.ActivityLogs.Add(
                new()
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
                .ActivityLogs.Where(x => x.UserId == user)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.CreatedAt, DateTime.UtcNow.AddDays(-8))
                );
        }

        ActivityLogRetentionCronJob job = new(
            factory,
            NullLogger<ActivityLogRetentionCronJob>.Instance,
            7
        );
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ActivityLogs.Should().BeEmpty();
    }

    [Fact]
    public void CronExpression_IsDailyAt3Am_AndJobNameIsSet()
    {
        ActivityLogRetentionCronJob job = new(
            CreateFactory(),
            NullLogger<ActivityLogRetentionCronJob>.Instance
        );

        job.CronExpression.Should().Be("0 3 * * *");
        job.JobName.Should().Be("Activity Log Retention");
    }
}
