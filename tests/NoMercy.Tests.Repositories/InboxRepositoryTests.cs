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
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Tests.Repositories;

// InboxRepository.ExecuteAssignmentAsync is a one-line delegation to
// InboxRoutingService.ExecuteAssignment (constructed from IStorageFactory +
// JobDispatcher). It carries no query/mapping logic of its own -- the requirement it
// would assert belongs to InboxRoutingService's own test surface -- so it is out of
// scope here; every other repository member is covered below.
public class InboxRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public InboxRepositoryTests()
    {
        _connection = new(connectionString: "Data Source=:memory:");
        _connection.Open();

        using (SqliteCommand fkOff = _connection.CreateCommand())
        {
            fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
            fkOff.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(connection: _connection).Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext OpenContext()
    {
        return new(options: _options);
    }

    private static InboxItem MakeItem(string status, Ulid? id = null)
    {
        return new()
        {
            Id = id ?? Ulid.NewUlid(),
            SourcePath = "/inbox/some-file.mkv",
            DriverId = Ulid.NewUlid(),
            DetectedType = "movie",
            Status = status,
        };
    }

    [Fact]
    public async Task GetAllAsync_NoStatusFilter_ReturnsEveryItem()
    {
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.AddRange(entities: [MakeItem(status: "NeedsReview"), MakeItem(status: "Done")]);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(context: queryCtx, routingService: null!);

        List<InboxItem> result = await repository.GetAllAsync(status: null);

        result.Should().HaveCount(expected: 2);
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ExcludesOtherStatuses()
    {
        await using MediaContext seedCtx = OpenContext();
        InboxItem needsReview = MakeItem(status: "NeedsReview");
        InboxItem done = MakeItem(status: "Done");
        seedCtx.InboxItems.AddRange(entities: [needsReview, done]);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(context: queryCtx, routingService: null!);

        List<InboxItem> result = await repository.GetAllAsync(status: "NeedsReview");

        result.Should().ContainSingle(predicate: i => i.Id == needsReview.Id);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByCreatedAtDescendingThenIdDescending()
    {
        await using MediaContext seedCtx = OpenContext();

        // Ulid.NewUlid() is only lexicographically sortable ACROSS milliseconds; two
        // Ulids minted in the same millisecond have an unordered random suffix, so
        // insertion order cannot stand in for "the lexicographically greater Id" below.
        // These two are fixed and pre-ordered so the tie-break assertion is
        // deterministic regardless of how fast the test host runs.
        Ulid lowerId = Ulid.Parse(base32: "01ARZ3NDEKTSV4RRFFQ69G5FAA");
        Ulid higherId = Ulid.Parse(base32: "01ARZ3NDEKTSV4RRFFQ69G5FBB");

        InboxItem tieOlder = MakeItem(status: "NeedsReview", id: lowerId);
        InboxItem tieNewer = MakeItem(status: "NeedsReview", id: higherId);
        InboxItem newest = MakeItem(status: "NeedsReview");

        seedCtx.InboxItems.Add(entity: tieOlder);
        await seedCtx.SaveChangesAsync();
        seedCtx.InboxItems.Add(entity: tieNewer);
        await seedCtx.SaveChangesAsync();
        seedCtx.InboxItems.Add(entity: newest);
        await seedCtx.SaveChangesAsync();

        DateTime anchor = new(year: 2026, month: 1, day: 1, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        await seedCtx.Database.ExecuteSqlInterpolatedAsync(
            sql: $"UPDATE InboxItems SET CreatedAt = {anchor} WHERE Id = {tieOlder.Id.ToString()}"
        );
        await seedCtx.Database.ExecuteSqlInterpolatedAsync(
            sql: $"UPDATE InboxItems SET CreatedAt = {anchor} WHERE Id = {tieNewer.Id.ToString()}"
        );
        await seedCtx.Database.ExecuteSqlInterpolatedAsync(
            sql: $"UPDATE InboxItems SET CreatedAt = {anchor.AddHours(value: 1)} WHERE Id = {newest.Id.ToString()}"
        );

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(context: queryCtx, routingService: null!);

        List<InboxItem> result = await repository.GetAllAsync(status: null);

        result.Should().HaveCount(expected: 3);
        result[index: 0].Id.Should().Be(expected: newest.Id, because: "the strictly newer CreatedAt must sort first");
        result[index: 1]
            .Id.Should()
            .Be(
                expected: tieNewer.Id,
                because: "of two equal CreatedAt values, the higher (Ulid, so lexicographically greater) Id must win the tie-break"
            );
        result[index: 2].Id.Should().Be(expected: tieOlder.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        InboxRepository repository = new(context: ctx, routingService: null!);

        InboxItem? result = await repository.GetByIdAsync(id: Ulid.NewUlid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAnUntrackedInstance()
    {
        InboxItem item = MakeItem(status: "NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.Add(entity: item);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(context: queryCtx, routingService: null!);

        InboxItem? result = await repository.GetByIdAsync(id: item.Id);

        result.Should().NotBeNull();
        queryCtx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ReturnsATrackedInstance_SoMutationsPersistOnSaveChanges()
    {
        InboxItem item = MakeItem(status: "NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.Add(entity: item);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(context: queryCtx, routingService: null!);

        InboxItem? tracked = await repository.GetTrackedByIdAsync(id: item.Id);
        tracked.Should().NotBeNull();
        tracked!.Status = "Imported";
        await queryCtx.SaveChangesAsync();

        await using MediaContext verifyCtx = OpenContext();
        InboxItem persisted = await verifyCtx.InboxItems.SingleAsync(predicate: i => i.Id == item.Id);
        persisted.Status.Should().Be(expected: "Imported");
    }

    [Fact]
    public async Task GetFolderByIdAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        InboxRepository repository = new(context: ctx, routingService: null!);

        Folder? result = await repository.GetFolderByIdAsync(folderId: Ulid.NewUlid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFolderByIdAsync_KnownId_ReturnsThatFolder()
    {
        Ulid folderId = Ulid.NewUlid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Folders.Add(entity: new() { Id = folderId, Path = "/media/inbox" });
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(context: queryCtx, routingService: null!);

        Folder? result = await repository.GetFolderByIdAsync(folderId: folderId);

        result.Should().NotBeNull();
        result!.Path.Should().Be(expected: "/media/inbox");
    }

    [Fact]
    public async Task DismissAsync_SetsStatusToDismissedAndPersistsIt()
    {
        InboxItem item = MakeItem(status: "NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.Add(entity: item);
        await seedCtx.SaveChangesAsync();

        await using MediaContext ctx = OpenContext();
        InboxItem tracked = await ctx.InboxItems.FirstAsync(predicate: i => i.Id == item.Id);
        InboxRepository repository = new(context: ctx, routingService: null!);

        await repository.DismissAsync(item: tracked);

        await using MediaContext verifyCtx = OpenContext();
        InboxItem persisted = await verifyCtx.InboxItems.SingleAsync(predicate: i => i.Id == item.Id);
        persisted.Status.Should().Be(expected: "Dismissed");
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheGivenItem()
    {
        InboxItem toDelete = MakeItem(status: "NeedsReview");
        InboxItem toKeep = MakeItem(status: "NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.AddRange(entities: [toDelete, toKeep]);
        await seedCtx.SaveChangesAsync();

        await using MediaContext ctx = OpenContext();
        InboxItem tracked = await ctx.InboxItems.FirstAsync(predicate: i => i.Id == toDelete.Id);
        InboxRepository repository = new(context: ctx, routingService: null!);

        await repository.DeleteAsync(item: tracked);

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.InboxItems.AnyAsync(predicate: i => i.Id == toDelete.Id)).Should().BeFalse();
        (await verifyCtx.InboxItems.AnyAsync(predicate: i => i.Id == toKeep.Id)).Should().BeTrue();
    }
}
