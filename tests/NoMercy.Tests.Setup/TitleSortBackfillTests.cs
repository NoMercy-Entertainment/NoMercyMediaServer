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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.Setup.Maintenance;

namespace NoMercy.Tests.Setup;

/// <summary>
/// Pins the behaviour that fixed music TitleSort not propagating: an algorithm
/// change must reach rows that already hold a (stale) value, not only null ones.
/// </summary>
[Trait("Category", "Unit")]
public class TitleSortBackfillTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public TitleSortBackfillTests()
    {
        _connection = new("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                _connection,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            .Options;

        using MediaContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private MediaContext CreateContext() => new(_options);

    [Fact]
    public async Task RunAsync_RecomputesDriftedValue_AndFillsNull()
    {
        Guid staleId = Guid.NewGuid();
        Guid nullId = Guid.NewGuid();

        await using (MediaContext ctx = CreateContext())
        {
            ctx.Artists.Add(
                new Artist
                {
                    Id = staleId,
                    Name = "The Beatles",
                    HostFolder = "a",
                    TitleSort = "value-from-an-older-algorithm",
                }
            );
            ctx.Artists.Add(
                new Artist
                {
                    Id = nullId,
                    Name = "A Perfect Circle",
                    HostFolder = "b",
                    TitleSort = null,
                }
            );
            await ctx.SaveChangesAsync();
        }

        await TitleSortBackfill.RunAsync(CreateContext, CancellationToken.None);

        await using (MediaContext ctx = CreateContext())
        {
            Artist drifted = await ctx.Artists.SingleAsync(a => a.Id == staleId);
            Artist wasNull = await ctx.Artists.SingleAsync(a => a.Id == nullId);

            // The stale value is replaced with the current algorithm's output
            // (leading article stripped, lower-cased), and the null is filled.
            drifted.TitleSort.Should().Be("The Beatles".TitleSort());
            drifted.TitleSort.Should().NotBe("value-from-an-older-algorithm");
            wasNull.TitleSort.Should().Be("A Perfect Circle".TitleSort());
        }
    }

    [Fact]
    public async Task RunAsync_LeavesUpToDateRowsUnchanged()
    {
        Guid id = Guid.NewGuid();
        string current = "The Beatles".TitleSort();

        await using (MediaContext ctx = CreateContext())
        {
            ctx.Artists.Add(
                new Artist
                {
                    Id = id,
                    Name = "The Beatles",
                    HostFolder = "a",
                    TitleSort = current,
                }
            );
            await ctx.SaveChangesAsync();
        }

        await TitleSortBackfill.RunAsync(CreateContext, CancellationToken.None);

        await using (MediaContext ctx = CreateContext())
        {
            Artist artist = await ctx.Artists.SingleAsync(a => a.Id == id);
            artist.TitleSort.Should().Be(current);
        }
    }

    // --- Albums: same requirement as Artists above, exercised separately since
    // ReconcileAlbumsAsync is its own method with its own batch/no-op logic. ---

    [Fact]
    public async Task RunAsync_Album_RecomputesDriftedValue_AndFillsNull()
    {
        Guid staleId = Guid.NewGuid();
        Guid nullId = Guid.NewGuid();
        (Ulid libraryId, Ulid folderId) = await SeedLibraryAndFolder();

        await using (MediaContext ctx = CreateContext())
        {
            ctx.Albums.Add(
                new Album
                {
                    Id = staleId,
                    Name = "The White Album",
                    TitleSort = "value-from-an-older-algorithm",
                    LibraryId = libraryId,
                    FolderId = folderId,
                    Library = null!,
                    LibraryFolder = null!,
                }
            );
            ctx.Albums.Add(
                new Album
                {
                    Id = nullId,
                    Name = "A Moon Shaped Pool",
                    TitleSort = null,
                    LibraryId = libraryId,
                    FolderId = folderId,
                    Library = null!,
                    LibraryFolder = null!,
                }
            );
            await ctx.SaveChangesAsync();
        }

        await TitleSortBackfill.RunAsync(CreateContext, CancellationToken.None);

        await using (MediaContext ctx = CreateContext())
        {
            Album drifted = await ctx.Albums.SingleAsync(a => a.Id == staleId);
            Album wasNull = await ctx.Albums.SingleAsync(a => a.Id == nullId);

            drifted.TitleSort.Should().Be("The White Album".TitleSort());
            drifted.TitleSort.Should().NotBe("value-from-an-older-algorithm");
            wasNull.TitleSort.Should().Be("A Moon Shaped Pool".TitleSort());
        }
    }

    [Fact]
    public async Task RunAsync_Album_LeavesUpToDateRowsUnchanged()
    {
        Guid id = Guid.NewGuid();
        string current = "The White Album".TitleSort();
        (Ulid libraryId, Ulid folderId) = await SeedLibraryAndFolder();

        await using (MediaContext ctx = CreateContext())
        {
            ctx.Albums.Add(
                new Album
                {
                    Id = id,
                    Name = "The White Album",
                    TitleSort = current,
                    LibraryId = libraryId,
                    FolderId = folderId,
                    Library = null!,
                    LibraryFolder = null!,
                }
            );
            await ctx.SaveChangesAsync();
        }

        await TitleSortBackfill.RunAsync(CreateContext, CancellationToken.None);

        await using (MediaContext ctx = CreateContext())
        {
            Album album = await ctx.Albums.SingleAsync(a => a.Id == id);
            album.TitleSort.Should().Be(current);
        }
    }

    /// <summary>
    /// Album requires a real Library + Folder (via Driver) row for its non-nullable
    /// FKs — mirrors the seeding pattern in MusicRepositoryTests rather than relying
    /// on Album's own default-constructed navigation properties, which EF would try
    /// to insert as brand-new rows and collide across multiple Albums in one SaveChanges.
    /// </summary>
    private async Task<(Ulid LibraryId, Ulid FolderId)> SeedLibraryAndFolder()
    {
        Ulid libraryId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        await using MediaContext ctx = CreateContext();
        ctx.Libraries.Add(
            new()
            {
                Id = libraryId,
                Title = "Music",
                Type = "music",
            }
        );
        ctx.Drivers.Add(
            new()
            {
                Id = driverId,
                Name = "Local Filesystem",
                Type = "local",
                Config = """{"rootPath":"/"}""",
            }
        );
        ctx.Folders.Add(
            new()
            {
                Id = folderId,
                Path = "/media/music",
                DriverId = driverId,
            }
        );
        await ctx.SaveChangesAsync();

        return (libraryId, folderId);
    }

    [Fact]
    public async Task RunAsync_NoRowsAtAll_CompletesWithoutError()
    {
        // Both Reconcile* methods must exit cleanly on their very first page check
        // (batch.Count == 0) when the library is empty — the common first-boot case.
        await TitleSortBackfill.RunAsync(CreateContext, CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_ContextFactoryThrows_LogsAndDoesNotThrow()
    {
        // RunAsync's outer try/catch must absorb a failure from the context factory
        // itself (e.g. a locked/corrupt DB file) rather than crashing the deferred
        // background job that calls it.
        await TitleSortBackfill.RunAsync(
            () => throw new InvalidOperationException("simulated context factory failure"),
            CancellationToken.None
        );
    }
}
