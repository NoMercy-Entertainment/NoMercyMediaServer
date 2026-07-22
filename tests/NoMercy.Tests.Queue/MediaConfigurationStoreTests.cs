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
[Trait(name: "Category", value: "Unit")]
public class MediaConfigurationStoreTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];

    private IDbContextFactory<AppDbContext> CreateFactory()
    {
        SqliteConnection connection = new(connectionString: "DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        _connections.Add(item: connection);

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection: connection)
            .Options;

        using (AppDbContext init = new(options: options))
        {
            init.Database.EnsureCreated();
        }

        Mock<IDbContextFactory<AppDbContext>> mock = new();
        mock.Setup(expression: x => x.CreateDbContext()).Returns(valueFunction: () => new(options: options));
        mock.Setup(expression: x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: options));
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
        MediaConfigurationStore store = new(contextFactory: CreateFactory());

        store.GetValue(key: "queue.extras.paused").Should().BeNull();
    }

    [Fact]
    public void HasKey_UnknownKey_ReturnsFalse()
    {
        MediaConfigurationStore store = new(contextFactory: CreateFactory());

        store.HasKey(key: "queue.extras.paused").Should().BeFalse();
    }

    [Fact]
    public void SetValue_NewKey_InsertsRow_HasKeyAndGetValueReflectIt()
    {
        MediaConfigurationStore store = new(contextFactory: CreateFactory());

        store.SetValue(key: "queue.extras.paused", value: "true");

        store.HasKey(key: "queue.extras.paused").Should().BeTrue();
        store.GetValue(key: "queue.extras.paused").Should().Be(expected: "true");
    }

    [Fact]
    public void SetValue_ExistingKey_UpdatesInPlace_DoesNotDuplicateRow()
    {
        MediaConfigurationStore store = new(contextFactory: CreateFactory());
        store.SetValue(key: "queue.extras.paused", value: "true");

        store.SetValue(key: "queue.extras.paused", value: "false");

        store.GetValue(key: "queue.extras.paused").Should().Be(expected: "false");
    }

    [Fact]
    public async Task SetValueAsync_NewKey_InsertsRow_StampsModifiedBy()
    {
        MediaConfigurationStore store = new(contextFactory: CreateFactory());
        Guid modifier = Guid.NewGuid();

        await store.SetValueAsync(key: "queue.image.paused", value: "true", modifiedBy: modifier);

        store.GetValue(key: "queue.image.paused").Should().Be(expected: "true");
    }

    [Fact]
    public async Task SetValueAsync_ExistingKey_UpdatesValueAndModifiedBy()
    {
        MediaConfigurationStore store = new(contextFactory: CreateFactory());
        await store.SetValueAsync(key: "queue.image.paused", value: "true", modifiedBy: Guid.NewGuid());
        Guid secondModifier = Guid.NewGuid();

        await store.SetValueAsync(key: "queue.image.paused", value: "false", modifiedBy: secondModifier);

        store.GetValue(key: "queue.image.paused").Should().Be(expected: "false");
    }

    [Fact]
    public async Task SetValueAsync_WithoutModifiedBy_LeavesItNull()
    {
        MediaConfigurationStore store = new(contextFactory: CreateFactory());

        await store.SetValueAsync(key: "queue.music.paused", value: "true");

        store.GetValue(key: "queue.music.paused").Should().Be(expected: "true");
    }
}
