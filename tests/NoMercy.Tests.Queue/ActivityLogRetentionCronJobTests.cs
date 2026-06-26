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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Queue.MediaServer.Jobs;
using Xunit;

namespace NoMercy.Tests.Queue;

public class ActivityLogRetentionCronJobTests
{
    private static IDbContextFactory<MediaContext> CreateFactory(string? db = null)
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(db ?? $"retention-{Guid.NewGuid()}")
            .Options;

        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MediaContext(options));
        return mock.Object;
    }

    [Fact]
    public async Task Deletes_rows_older_than_retention_window()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.ActivityLogs.AddRange(
                new ActivityLog
                {
                    Category = ActivityCategory.Connection,
                    Time = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow.AddDays(-31),
                    DeviceId = Ulid.NewUlid(),
                    UserId = Guid.NewGuid(),
                },
                new ActivityLog
                {
                    Category = ActivityCategory.Connection,
                    Time = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    DeviceId = Ulid.NewUlid(),
                    UserId = Guid.NewGuid(),
                }
            );
            await seed.SaveChangesAsync();
        }

        ActivityLogRetentionCronJob job = new(
            factory,
            NullLogger<ActivityLogRetentionCronJob>.Instance,
            retentionDays: 30
        );
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ActivityLogs.Should().HaveCount(1);
        ctx.ActivityLogs.Single().CreatedAt.Should().BeAfter(DateTime.UtcNow.AddDays(-30));
    }

    [Fact]
    public async Task Respects_configurable_retention_days()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.ActivityLogs.Add(
                new ActivityLog
                {
                    Category = ActivityCategory.Connection,
                    Time = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow.AddDays(-8),
                    DeviceId = Ulid.NewUlid(),
                    UserId = Guid.NewGuid(),
                }
            );
            await seed.SaveChangesAsync();
        }

        ActivityLogRetentionCronJob job = new(
            factory,
            NullLogger<ActivityLogRetentionCronJob>.Instance,
            retentionDays: 7
        );
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ActivityLogs.Should().BeEmpty();
    }
}
