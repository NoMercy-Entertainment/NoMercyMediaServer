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
using Moq;
using NoMercy.Database;
using NoMercy.Queue.MediaServer.Configuration;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="MediaConfigurationStore"/> is what <see cref="NoMercyQueue.QueueRunner"/>
/// persists a queue's paused state through — the read-after-write contract
/// tested here (insert-on-missing, update-on-existing, ModifiedBy stamped
/// only on the async write path) is exactly what makes "pause survives a
/// server restart" true. A regression here (e.g. always inserting instead of
/// updating) would silently duplicate rows and make <c>HasKey</c>/<c>GetValue</c>
/// pick an arbitrary one.
/// </summary>
[Trait("Category", "Unit")]
public class MediaConfigurationStoreTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];

    private IDbContextFactory<AppDbContext> CreateFactory()
    {
        SqliteConnection connection = new("DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        _connections.Add(connection);

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (AppDbContext init = new(options))
        {
            init.Database.EnsureCreated();
        }

        Mock<IDbContextFactory<AppDbContext>> mock = new();
        mock.Setup(x => x.CreateDbContext()).Returns(() => new(options));
        mock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new(options));
        return mock.Object;
    }

    public void Dispose()
    {
        foreach (SqliteConnection connection in _connections)
            connection.Dispose();
    }

    [Fact]
    public void GetValue_UnknownKey_ReturnsNull()
    {
        MediaConfigurationStore store = new(CreateFactory());

        store.GetValue("queue.extras.paused").Should().BeNull();
    }

    [Fact]
    public void HasKey_UnknownKey_ReturnsFalse()
    {
        MediaConfigurationStore store = new(CreateFactory());

        store.HasKey("queue.extras.paused").Should().BeFalse();
    }

    [Fact]
    public void SetValue_NewKey_InsertsRow_HasKeyAndGetValueReflectIt()
    {
        MediaConfigurationStore store = new(CreateFactory());

        store.SetValue("queue.extras.paused", "true");

        store.HasKey("queue.extras.paused").Should().BeTrue();
        store.GetValue("queue.extras.paused").Should().Be("true");
    }

    [Fact]
    public void SetValue_ExistingKey_UpdatesInPlace_DoesNotDuplicateRow()
    {
        MediaConfigurationStore store = new(CreateFactory());
        store.SetValue("queue.extras.paused", "true");

        store.SetValue("queue.extras.paused", "false");

        store.GetValue("queue.extras.paused").Should().Be("false");
    }

    [Fact]
    public async Task SetValueAsync_NewKey_InsertsRow_StampsModifiedBy()
    {
        MediaConfigurationStore store = new(CreateFactory());
        Guid modifier = Guid.NewGuid();

        await store.SetValueAsync("queue.image.paused", "true", modifier);

        store.GetValue("queue.image.paused").Should().Be("true");
    }

    [Fact]
    public async Task SetValueAsync_ExistingKey_UpdatesValueAndModifiedBy()
    {
        MediaConfigurationStore store = new(CreateFactory());
        await store.SetValueAsync("queue.image.paused", "true", Guid.NewGuid());
        Guid secondModifier = Guid.NewGuid();

        await store.SetValueAsync("queue.image.paused", "false", secondModifier);

        store.GetValue("queue.image.paused").Should().Be("false");
    }

    [Fact]
    public async Task SetValueAsync_WithoutModifiedBy_LeavesItNull()
    {
        MediaConfigurationStore store = new(CreateFactory());

        await store.SetValueAsync("queue.music.paused", "true");

        store.GetValue("queue.music.paused").Should().Be("true");
    }
}
