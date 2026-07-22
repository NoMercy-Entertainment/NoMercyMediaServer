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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.NmSystem.Auth;
using NoMercy.Service.Jobs;
using NoMercy.Service.Seeds;
using NoMercy.Storage;
using NoMercy.Tests.Service.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Service.Jobs;

/// <summary>
/// <see cref="ServerUserSyncCronJob"/> is the ONLY path that re-syncs the local
/// Users table after first boot (<see cref="UsersSeed"/> only ever runs once).
/// These pin the three real decisions it makes: never call upstream without a
/// token, never refresh the auth allow-list cache on a skipped/no-op sync (that
/// cache refresh is what makes an accepted invite grant access immediately —
/// running it unconditionally would just be wasted DB round-trips on every
/// 5-minute tick), and never crash the cron worker regardless of outcome.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ServerUserSyncCronJobTests
{
    private static SqliteMediaContextFactory NewContextFactory()
    {
        return new();
    }

    [Fact]
    public async Task ExecuteAsync_NoAccessToken_SkipsSyncEntirely()
    {
        Mock<IServerUserSyncService> syncService = new();
        Mock<IAuthTokenStore> tokenStore = new();
        tokenStore.SetupGet(expression: t => t.AccessToken).Returns(value: (string?)null);
        Mock<IUserCache> userCache = new();
        await using SqliteMediaContextFactory contextFactory = NewContextFactory();

        ServerUserSyncCronJob job = new(
            contextFactory: contextFactory,
            syncService: syncService.Object,
            storage: Mock.Of<IStorage>(),
            authTokenStore: tokenStore.Object,
            userCache: userCache.Object,
            logger: NullLogger<ServerUserSyncCronJob>.Instance
        );

        await job.ExecuteAsync(parameters: string.Empty);

        syncService.Verify(
            expression: s =>
                s.SyncAsync(
                    It.IsAny<MediaContext>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
        userCache.Verify(expression: c => c.RefreshUsersAsync(It.IsAny<MediaContext>()), times: Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SyncNotAttempted_DoesNotRefreshUserCache()
    {
        Mock<IServerUserSyncService> syncService = new();
        syncService
            .Setup(expression: s =>
                s.SyncAsync(
                    It.IsAny<MediaContext>(),
                    It.IsAny<IStorage>(),
                    "token",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ServerUserSyncResult(Attempted: false, UpstreamUserCount: 0, RevokedCount: 0));
        Mock<IAuthTokenStore> tokenStore = new();
        tokenStore.SetupGet(expression: t => t.AccessToken).Returns(value: "token");
        Mock<IUserCache> userCache = new();
        await using SqliteMediaContextFactory contextFactory = NewContextFactory();

        ServerUserSyncCronJob job = new(
            contextFactory: contextFactory,
            syncService: syncService.Object,
            storage: Mock.Of<IStorage>(),
            authTokenStore: tokenStore.Object,
            userCache: userCache.Object,
            logger: NullLogger<ServerUserSyncCronJob>.Instance
        );

        await job.ExecuteAsync(parameters: string.Empty);

        userCache.Verify(expression: c => c.RefreshUsersAsync(It.IsAny<MediaContext>()), times: Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SyncAttempted_RefreshesUserCache()
    {
        Mock<IServerUserSyncService> syncService = new();
        syncService
            .Setup(expression: s =>
                s.SyncAsync(
                    It.IsAny<MediaContext>(),
                    It.IsAny<IStorage>(),
                    "token",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ServerUserSyncResult(Attempted: true, UpstreamUserCount: 3, RevokedCount: 0));
        Mock<IAuthTokenStore> tokenStore = new();
        tokenStore.SetupGet(expression: t => t.AccessToken).Returns(value: "token");
        Mock<IUserCache> userCache = new();
        await using SqliteMediaContextFactory contextFactory = NewContextFactory();

        ServerUserSyncCronJob job = new(
            contextFactory: contextFactory,
            syncService: syncService.Object,
            storage: Mock.Of<IStorage>(),
            authTokenStore: tokenStore.Object,
            userCache: userCache.Object,
            logger: NullLogger<ServerUserSyncCronJob>.Instance
        );

        await job.ExecuteAsync(parameters: string.Empty);

        userCache.Verify(expression: c => c.RefreshUsersAsync(It.IsAny<MediaContext>()), times: Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RevokedUsers_LogsAtInformation()
    {
        Mock<IServerUserSyncService> syncService = new();
        syncService
            .Setup(expression: s =>
                s.SyncAsync(
                    It.IsAny<MediaContext>(),
                    It.IsAny<IStorage>(),
                    "token",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ServerUserSyncResult(Attempted: true, UpstreamUserCount: 5, RevokedCount: 2));
        Mock<IAuthTokenStore> tokenStore = new();
        tokenStore.SetupGet(expression: t => t.AccessToken).Returns(value: "token");
        Mock<IUserCache> userCache = new();
        Mock<ILogger<ServerUserSyncCronJob>> logger = new();
        await using SqliteMediaContextFactory contextFactory = NewContextFactory();

        ServerUserSyncCronJob job = new(
            contextFactory: contextFactory,
            syncService: syncService.Object,
            storage: Mock.Of<IStorage>(),
            authTokenStore: tokenStore.Object,
            userCache: userCache.Object,
            logger: logger.Object
        );

        await job.ExecuteAsync(parameters: string.Empty);

        logger.Verify(
            expression: l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_NoRevokedUsers_LogsAtDebug()
    {
        Mock<IServerUserSyncService> syncService = new();
        syncService
            .Setup(expression: s =>
                s.SyncAsync(
                    It.IsAny<MediaContext>(),
                    It.IsAny<IStorage>(),
                    "token",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ServerUserSyncResult(Attempted: true, UpstreamUserCount: 5, RevokedCount: 0));
        Mock<IAuthTokenStore> tokenStore = new();
        tokenStore.SetupGet(expression: t => t.AccessToken).Returns(value: "token");
        Mock<IUserCache> userCache = new();
        Mock<ILogger<ServerUserSyncCronJob>> logger = new();
        await using SqliteMediaContextFactory contextFactory = NewContextFactory();

        ServerUserSyncCronJob job = new(
            contextFactory: contextFactory,
            syncService: syncService.Object,
            storage: Mock.Of<IStorage>(),
            authTokenStore: tokenStore.Object,
            userCache: userCache.Object,
            logger: logger.Object
        );

        await job.ExecuteAsync(parameters: string.Empty);

        logger.Verify(
            expression: l =>
                l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public void CronExpression_RunsEveryFiveMinutes()
    {
        ServerUserSyncCronJob job = new(
            contextFactory: NewContextFactory(),
            syncService: Mock.Of<IServerUserSyncService>(),
            storage: Mock.Of<IStorage>(),
            authTokenStore: Mock.Of<IAuthTokenStore>(),
            userCache: Mock.Of<IUserCache>(),
            logger: NullLogger<ServerUserSyncCronJob>.Instance
        );

        job.CronExpression.Should().Be(expected: "*/5 * * * *");
    }

    [Fact]
    public void JobName_IsHumanReadable()
    {
        ServerUserSyncCronJob job = new(
            contextFactory: NewContextFactory(),
            syncService: Mock.Of<IServerUserSyncService>(),
            storage: Mock.Of<IStorage>(),
            authTokenStore: Mock.Of<IAuthTokenStore>(),
            userCache: Mock.Of<IUserCache>(),
            logger: NullLogger<ServerUserSyncCronJob>.Instance
        );

        job.JobName.Should().Be(expected: "Server User Sync");
    }
}
