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

using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Service.Seeds;
using NoMercy.Tests.Common.Providers;

namespace NoMercy.Tests.Service.Seeds;

/// <summary>
/// Requirement: <see cref="MusicGenresSeed.Init"/> must check the local DB BEFORE
/// touching MusicBrainz — mirroring the sibling TMDB seeds (Languages/Countries/
/// Genres/Certifications), which all check the DB first. The previous shape fetched
/// MusicBrainz's first page unconditionally to learn the "expected" count for a
/// comparison, paying a rate-limited round-trip on the pre-auth blocking boot path
/// on EVERY boot — even once fully seeded — just to learn there was nothing to do.
/// </summary>
[Trait("Category", "Unit")]
[Collection("HttpClientProvider")]
public sealed class MusicGenresSeedTests : ProviderHttpHarness
{
    public MusicGenresSeedTests()
        : base(HttpClientNames.MusicBrainz) { }

    private static MediaContext BuildContext(SqliteConnection connection)
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connection,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            .Options;
        MediaContext context = new(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Init_GenresAlreadySeeded_SkipsMusicBrainzEntirely()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        await using (MediaContext seedContext = BuildContext(connection))
        {
            seedContext.MusicGenres.Add(new() { Id = Guid.NewGuid(), Name = "ambient" });
            await seedContext.SaveChangesAsync();
        }

        await using MediaContext context = BuildContext(connection);
        await MusicGenresSeed.Init(context);

        Handler
            .Requests.Should()
            .BeEmpty("an already-seeded local DB must never pay a MusicBrainz round-trip");
    }

    [Fact]
    public async Task Init_NoGenresYet_FetchesFirstPageAndSeedsFromMusicBrainz()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        Guid genreId = Guid.NewGuid();
        MusicBrainzAllGenres firstPage = new()
        {
            Genres = [new() { Id = genreId, Name = "downtempo" }],
            GenreCount = 1,
        };
        Handler.WhenGet("genre/all", MockResponse.Json(HttpStatusCode.OK, firstPage));

        await using MediaContext context = BuildContext(connection);
        await MusicGenresSeed.Init(context);

        MusicGenre? seeded = await context.MusicGenres.FirstOrDefaultAsync(g => g.Id == genreId);
        seeded.Should().NotBeNull();
        seeded!.Name.Should().Be("downtempo");

        Handler
            .Requests.Should()
            .ContainSingle(
                "a genuinely empty local DB must still fetch from MusicBrainz on first boot"
            );
    }

    [Fact]
    public async Task Init_MusicBrainzUnreachable_DoesNotThrow_LeavesGenresEmpty()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        Handler.WhenGet(
            "genre/all",
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        );

        await using MediaContext context = BuildContext(connection);
        await MusicGenresSeed.Init(context);

        (await context.MusicGenres.CountAsync()).Should().Be(0);
    }
}
