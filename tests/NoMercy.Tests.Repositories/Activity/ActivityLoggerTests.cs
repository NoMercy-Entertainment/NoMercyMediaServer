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
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Tests.Repositories.Activity;

public class ActivityLoggerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public ActivityLoggerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using (SqliteCommand fkOff = _connection.CreateCommand())
        {
            fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
            fkOff.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(_connection).Options;

        using MediaContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private IDbContextFactory<MediaContext> CreateFactory()
    {
        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MediaContext(_options));
        return mock.Object;
    }

    [Fact]
    public async Task LogAuthAsync_writes_row_with_auth_category_and_success()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        ActivityLogger logger = new(
            factory,
            NullLogger<ActivityLogger>.Instance,
            hubBroadcaster: null
        );
        Guid userId = Guid.NewGuid();
        Ulid deviceId = Ulid.NewUlid();

        await logger.LogAuthAsync("auth.login", userId, deviceId, success: true);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ActivityLog row = ctx.ActivityLogs.Single();
        row.Category.Should().Be(ActivityCategory.Auth);
        row.Type.Should().Be("auth.login");
        row.UserId.Should().Be(userId);
        row.DeviceId.Should().Be(deviceId);
        row.Success.Should().BeTrue();
        row.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task LogConnectionAsync_writes_row_with_connection_category()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        ActivityLogger logger = new(
            factory,
            NullLogger<ActivityLogger>.Instance,
            hubBroadcaster: null
        );

        await logger.LogConnectionAsync("connection.connected", Guid.NewGuid(), Ulid.NewUlid());

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ActivityLogs.Single().Category.Should().Be(ActivityCategory.Connection);
    }

    [Fact]
    public async Task LogPlaybackAsync_serializes_metadata_to_json()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        ActivityLogger logger = new(
            factory,
            NullLogger<ActivityLogger>.Instance,
            hubBroadcaster: null
        );
        Ulid mediaId = Ulid.NewUlid();
        object metadata = new { title = "Heat", duration_ms = 9_900_000 };

        await logger.LogPlaybackAsync(
            "playback.started",
            Guid.NewGuid(),
            Ulid.NewUlid(),
            mediaId,
            metadata
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ActivityLog row = ctx.ActivityLogs.Single();
        row.Category.Should().Be(ActivityCategory.Playback);
        row.MediaId.Should().Be(mediaId);
        row.Metadata.Should().Contain("\"title\":\"Heat\"");
        row.Metadata.Should().Contain("\"duration_ms\":9900000");
    }

    [Fact]
    public async Task LogConfigurationAsync_packs_key_old_new_into_metadata()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        ActivityLogger logger = new(
            factory,
            NullLogger<ActivityLogger>.Instance,
            hubBroadcaster: null
        );

        await logger.LogConfigurationAsync(
            "config.server_changed",
            Guid.NewGuid(),
            Ulid.NewUlid(),
            configKey: "encoder.default_profile",
            oldValue: "x264",
            newValue: "x265"
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ActivityLog row = ctx.ActivityLogs.Single();
        row.Category.Should().Be(ActivityCategory.Configuration);
        row.Metadata.Should().Contain("\"key\":\"encoder.default_profile\"");
        row.Metadata.Should().Contain("\"old_value\":\"x264\"");
        row.Metadata.Should().Contain("\"new_value\":\"x265\"");
    }

    [Fact]
    public async Task LogFailureAsync_records_error_code_and_message()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        ActivityLogger logger = new(
            factory,
            NullLogger<ActivityLogger>.Instance,
            hubBroadcaster: null
        );

        await logger.LogFailureAsync(
            "failure.playback_start",
            Guid.NewGuid(),
            Ulid.NewUlid(),
            errorCode: "transcoder_unavailable",
            message: "FFmpeg returned exit code 2"
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ActivityLog row = ctx.ActivityLogs.Single();
        row.Category.Should().Be(ActivityCategory.Failure);
        row.Success.Should().BeFalse();
        row.ErrorCode.Should().Be("transcoder_unavailable");
        row.Metadata.Should().Contain("\"message\":\"FFmpeg returned exit code 2\"");
    }

    [Fact]
    public async Task Successful_write_invokes_hub_broadcaster()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Mock<IActivityHubBroadcaster> broadcaster = new();
        broadcaster
            .Setup(b => b.BroadcastAsync(It.IsAny<ActivityLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ActivityLogger logger = new(
            factory,
            NullLogger<ActivityLogger>.Instance,
            broadcaster.Object
        );
        await logger.LogAuthAsync("auth.login", Guid.NewGuid(), Ulid.NewUlid(), success: true);

        broadcaster.Verify(
            b =>
                b.BroadcastAsync(
                    It.Is<ActivityLog>(r => r.Type == "auth.login"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Broadcast_exception_does_not_throw_to_caller()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Mock<IActivityHubBroadcaster> broadcaster = new();
        broadcaster
            .Setup(b => b.BroadcastAsync(It.IsAny<ActivityLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        ActivityLogger logger = new(
            factory,
            NullLogger<ActivityLogger>.Instance,
            broadcaster.Object
        );

        Func<Task> act = () =>
            logger.LogAuthAsync("auth.login", Guid.NewGuid(), Ulid.NewUlid(), success: true);

        await act.Should().NotThrowAsync();
    }
}
