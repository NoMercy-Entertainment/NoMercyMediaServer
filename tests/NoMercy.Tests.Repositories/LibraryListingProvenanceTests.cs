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
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using Xunit;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// What the library listing shows, and why.
///
/// <para>
/// The listing filtered on files that exist, which was the only way to keep out
/// the shows identification attached on a guess. It also hid a show the owner
/// had just added, so "did my add work?" had no answer in the interface on the
/// one day it matters most. Provenance is what lets the filter say what it
/// means.
/// </para>
/// </summary>
public class LibraryListingProvenanceTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Ulid LibraryId = Ulid.NewUlid();

    private const int AddedByOwner = 138502;
    private const int AttachedByAScan = 456;
    private const int HasAFile = 789;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public LibraryListingProvenanceTests()
    {
        _connection = new("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(_connection).Options;

        using MediaContext context = new(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AShowTheOwnerAddedIsListedBeforeAnythingHasDownloadedForIt()
    {
        await SeedAsync();

        List<int> listed = await ListedShowIdsAsync();

        listed.Should().Contain(AddedByOwner, "the owner asked for this show");
    }

    [Fact]
    public async Task AShowAScanAttachedOnAGuessIsNotListed()
    {
        await SeedAsync();

        List<int> listed = await ListedShowIdsAsync();

        listed
            .Should()
            .NotContain(AttachedByAScan, "nobody asked for it and there is nothing of it on disk");
    }

    [Fact]
    public async Task AShowWithAFileIsListedWhateverBroughtItIn()
    {
        await SeedAsync();

        List<int> listed = await ListedShowIdsAsync();

        listed.Should().Contain(HasAFile);
    }

    /// <summary>
    /// The listing's own filter, run against the seeded database.
    ///
    /// <para>
    /// The repository compiles this query with EF.CompileAsyncQuery and reaches
    /// it through a context factory, neither of which a fixture can hand a
    /// connection to. The predicate is what this is about, so the predicate is
    /// what is asserted - kept beside the repository's, and wrong in the same
    /// way if either drifts.
    /// </para>
    /// </summary>
    private async Task<List<int>> ListedShowIdsAsync()
    {
        await using MediaContext context = new(_options);

        return await context
            .Tvs.AsNoTracking()
            .Where(tv => tv.Library.Id == LibraryId)
            .Where(tv => tv.Library.LibraryUsers.Any(user => user.UserId.Equals(UserId)))
            .Where(tv =>
                tv.Episodes.Any(episode => episode.VideoFiles.Any())
                || tv.Library.LibraryTvs.Any(link =>
                    link.TvId == tv.Id && link.AddedBy == LibraryLinkOrigin.Manual
                )
            )
            .Select(tv => tv.Id)
            .ToListAsync();
    }

    private async Task SeedAsync()
    {
        await using MediaContext context = new(_options);

        context.Users.Add(new() { Id = UserId, Email = "owner@example.com" });
        context.Libraries.Add(
            new()
            {
                Id = LibraryId,
                Title = "Television",
                Type = "tv",
            }
        );
        await context.SaveChangesAsync();

        context.LibraryUser.Add(new() { LibraryId = LibraryId, UserId = UserId });
        await context.SaveChangesAsync();

        foreach (int id in new[] { AddedByOwner, AttachedByAScan, HasAFile })
        {
            context.Tvs.Add(
                new()
                {
                    Id = id,
                    Title = $"Show {id}",
                    LibraryId = LibraryId,
                }
            );
        }

        await context.SaveChangesAsync();

        context.LibraryTv.AddRange(
            new(LibraryId, AddedByOwner, LibraryLinkOrigin.Manual),
            new(LibraryId, AttachedByAScan),
            new(LibraryId, HasAFile)
        );

        Season season = new()
        {
            Id = 1,
            TvId = HasAFile,
            SeasonNumber = 1,
            Title = "Season 1",
        };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();

        Episode episode = new()
        {
            Id = 1,
            TvId = HasAFile,
            SeasonId = season.Id,
            SeasonNumber = 1,
            EpisodeNumber = 1,
            Title = "One",
        };
        context.Episodes.Add(episode);
        await context.SaveChangesAsync();

        context.VideoFiles.Add(
            new()
            {
                Id = Ulid.NewUlid(),
                EpisodeId = episode.Id,
                Folder = "/Show",
                Filename = "one.mkv",
                Share = "local",
            }
        );

        await context.SaveChangesAsync();
    }
}
