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
        _connection = new("Data Source=:memory:");
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

    private MediaContext OpenContext()
    {
        return new(_options);
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
        seedCtx.InboxItems.AddRange(MakeItem("NeedsReview"), MakeItem("Done"));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(queryCtx, null!);

        List<InboxItem> result = await repository.GetAllAsync(status: null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ExcludesOtherStatuses()
    {
        await using MediaContext seedCtx = OpenContext();
        InboxItem needsReview = MakeItem("NeedsReview");
        InboxItem done = MakeItem("Done");
        seedCtx.InboxItems.AddRange(needsReview, done);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(queryCtx, null!);

        List<InboxItem> result = await repository.GetAllAsync(status: "NeedsReview");

        result.Should().ContainSingle(i => i.Id == needsReview.Id);
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
        Ulid lowerId = Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAA");
        Ulid higherId = Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FBB");

        InboxItem tieOlder = MakeItem("NeedsReview", lowerId);
        InboxItem tieNewer = MakeItem("NeedsReview", higherId);
        InboxItem newest = MakeItem("NeedsReview");

        seedCtx.InboxItems.Add(tieOlder);
        await seedCtx.SaveChangesAsync();
        seedCtx.InboxItems.Add(tieNewer);
        await seedCtx.SaveChangesAsync();
        seedCtx.InboxItems.Add(newest);
        await seedCtx.SaveChangesAsync();

        DateTime anchor = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await seedCtx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InboxItems SET CreatedAt = {anchor} WHERE Id = {tieOlder.Id.ToString()}"
        );
        await seedCtx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InboxItems SET CreatedAt = {anchor} WHERE Id = {tieNewer.Id.ToString()}"
        );
        await seedCtx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InboxItems SET CreatedAt = {anchor.AddHours(1)} WHERE Id = {newest.Id.ToString()}"
        );

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(queryCtx, null!);

        List<InboxItem> result = await repository.GetAllAsync(status: null);

        result.Should().HaveCount(3);
        result[0].Id.Should().Be(newest.Id, "the strictly newer CreatedAt must sort first");
        result[1]
            .Id.Should()
            .Be(
                tieNewer.Id,
                "of two equal CreatedAt values, the higher (Ulid, so lexicographically greater) Id must win the tie-break"
            );
        result[2].Id.Should().Be(tieOlder.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        InboxRepository repository = new(ctx, null!);

        InboxItem? result = await repository.GetByIdAsync(Ulid.NewUlid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAnUntrackedInstance()
    {
        InboxItem item = MakeItem("NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.Add(item);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(queryCtx, null!);

        InboxItem? result = await repository.GetByIdAsync(item.Id);

        result.Should().NotBeNull();
        queryCtx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ReturnsATrackedInstance_SoMutationsPersistOnSaveChanges()
    {
        InboxItem item = MakeItem("NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.Add(item);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(queryCtx, null!);

        InboxItem? tracked = await repository.GetTrackedByIdAsync(item.Id);
        tracked.Should().NotBeNull();
        tracked!.Status = "Imported";
        await queryCtx.SaveChangesAsync();

        await using MediaContext verifyCtx = OpenContext();
        InboxItem persisted = await verifyCtx.InboxItems.SingleAsync(i => i.Id == item.Id);
        persisted.Status.Should().Be("Imported");
    }

    [Fact]
    public async Task GetFolderByIdAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        InboxRepository repository = new(ctx, null!);

        Folder? result = await repository.GetFolderByIdAsync(Ulid.NewUlid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFolderByIdAsync_KnownId_ReturnsThatFolder()
    {
        Ulid folderId = Ulid.NewUlid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Folders.Add(new() { Id = folderId, Path = "/media/inbox" });
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        InboxRepository repository = new(queryCtx, null!);

        Folder? result = await repository.GetFolderByIdAsync(folderId);

        result.Should().NotBeNull();
        result!.Path.Should().Be("/media/inbox");
    }

    [Fact]
    public async Task DismissAsync_SetsStatusToDismissedAndPersistsIt()
    {
        InboxItem item = MakeItem("NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.Add(item);
        await seedCtx.SaveChangesAsync();

        await using MediaContext ctx = OpenContext();
        InboxItem tracked = await ctx.InboxItems.FirstAsync(i => i.Id == item.Id);
        InboxRepository repository = new(ctx, null!);

        await repository.DismissAsync(tracked);

        await using MediaContext verifyCtx = OpenContext();
        InboxItem persisted = await verifyCtx.InboxItems.SingleAsync(i => i.Id == item.Id);
        persisted.Status.Should().Be("Dismissed");
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheGivenItem()
    {
        InboxItem toDelete = MakeItem("NeedsReview");
        InboxItem toKeep = MakeItem("NeedsReview");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.InboxItems.AddRange(toDelete, toKeep);
        await seedCtx.SaveChangesAsync();

        await using MediaContext ctx = OpenContext();
        InboxItem tracked = await ctx.InboxItems.FirstAsync(i => i.Id == toDelete.Id);
        InboxRepository repository = new(ctx, null!);

        await repository.DeleteAsync(tracked);

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.InboxItems.AnyAsync(i => i.Id == toDelete.Id)).Should().BeFalse();
        (await verifyCtx.InboxItems.AnyAsync(i => i.Id == toKeep.Id)).Should().BeTrue();
    }
}
