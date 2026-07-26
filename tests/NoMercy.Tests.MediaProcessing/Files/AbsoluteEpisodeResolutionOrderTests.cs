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
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFileLibrary;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Domain;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Tests.Common.Providers;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// An absolute episode number means whatever the provider's absolute ordering says it
/// means, so that ordering has to be consulted before anything is inferred locally.
///
/// The local fallback indexes the show's own episodes as one flat run, which returns a row
/// for any number inside its range. That always looks like a match, so if it runs first the
/// group lookup never happens and a wrong episode is imported silently. These tests pin the
/// order by making the two answers disagree.
/// </summary>
[Collection("HttpClientProvider")]
public class AbsoluteEpisodeResolutionOrderTests : ProviderHttpHarness
{
    private const int ShowId = 771001;
    private const string ShowName = "Ordering Probe";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public AbsoluteEpisodeResolutionOrderTests()
        : base("TMDB")
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

    public override void Dispose()
    {
        _connection.Dispose();
        base.Dispose();

        // The harness resets the process-wide HTTP provider on the way out, but this assembly
        // installs its TMDB mock exactly once for every test in it. Without putting that back,
        // every test running after this class loses its HTTP factory and its provider lookups
        // silently stop happening.
        HttpClientProvider.Initialize(new TmdbMockHttpClientFactory());

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Season one is deliberately missing an episode, which is ordinary for a library that
    /// has not finished scanning. That gap shifts every later entry in the flat run, so the
    /// two strategies land on different episodes for the same absolute number and the test
    /// can tell which one answered.
    /// </summary>
    private void SeedShowWithAGapInSeasonOne()
    {
        using MediaContext ctx = new(_options);

        ctx.Tvs.Add(new() { Id = ShowId, Title = ShowName });
        ctx.Seasons.Add(
            new()
            {
                Id = 1,
                TvId = ShowId,
                SeasonNumber = 1,
            }
        );
        ctx.Seasons.Add(
            new()
            {
                Id = 2,
                TvId = ShowId,
                SeasonNumber = 2,
            }
        );

        int episodeId = 1;
        foreach (int number in Enumerable.Range(1, 12).Where(number => number != 5))
            ctx.Episodes.Add(
                new()
                {
                    Id = episodeId++,
                    TvId = ShowId,
                    SeasonId = 1,
                    SeasonNumber = 1,
                    EpisodeNumber = number,
                    Title = $"S01E{number:D2}",
                }
            );

        foreach (int number in Enumerable.Range(1, 12))
            ctx.Episodes.Add(
                new()
                {
                    Id = 100 + number,
                    TvId = ShowId,
                    SeasonId = 2,
                    SeasonNumber = 2,
                    EpisodeNumber = number,
                    Title = $"S02E{number:D2}",
                }
            );

        ctx.SaveChanges();
    }

    private void ScriptTmdb()
    {
        Handler.WhenGet(
            "/search/tv",
            MockResponse.Json(
                HttpStatusCode.OK,
                $$"""
                {"page":1,"total_pages":1,"total_results":1,"results":[
                  {"id":{{ShowId}},"name":"{{ShowName}}","first_air_date":"2020-01-01"}
                ]}
                """
            )
        );

        Handler.WhenGet(
            $"/tv/{ShowId}/episode_groups",
            MockResponse.Json(
                HttpStatusCode.OK,
                """
                {"id":1,"results":[
                  {"id":"grp-absolute","name":"Absolute","order":1,"type":2,"episode_count":24,"group_count":1}
                ]}
                """
            )
        );

        // Absolute 13 is the first episode of season two in this ordering. The flat local run
        // would reach season two episode two instead, because season one is short a file.
        Handler.WhenGet(
            "/tv/episode_group/grp-absolute",
            MockResponse.Json(
                HttpStatusCode.OK,
                $$"""
                {"id":"grp-absolute","name":"Absolute","type":2,"episode_count":24,"group_count":1,
                 "description":"","groups":[{"id":"g1","name":"Absolute","order":1,"locked":false,"episodes":[
                  {{EpisodeRun(1, 1, 12)}},
                  {{EpisodeRun(2, 13, 12)}}
                 ]}]}
                """
            )
        );
    }

    private static string EpisodeRun(int seasonNumber, int startId, int count) =>
        string.Join(
            ",",
            Enumerable
                .Range(1, count)
                .Select(number =>
                    $$"""
                    {"id":{{startId
                        + number
                        - 1}},"episode_number":{{number}},"season_number":{{seasonNumber}},"name":"S{{seasonNumber:D2}}E{{number:D2}}","overview":"","air_date":null,"order":{{startId
                        + number
                        - 2}}}
                    """
                )
        );

    private MediaIdentificationService BuildService(MediaContext context)
    {
        ServiceCollection services = new();
        return new(
            context,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>()
        );
    }

    [Fact]
    public async Task AbsoluteNumber_IsResolvedByTheProviderOrdering_NotTheLocalFlatRun()
    {
        SeedShowWithAGapInSeasonOne();
        ScriptTmdb();

        await using MediaContext context = new(_options);
        MediaIdentificationService service = BuildService(context);

        MovieFile parsed = new("/downloads/Ordering Probe - 13.mkv")
        {
            Title = ShowName,
            Season = 1,
            Episode = 13,
            IsSeries = true,
            IsSuccess = true,
        };

        (MovieOrEpisode match, string? imdbId)? result = await service.IdentifyAsync(
            parsed,
            MediaTypes.AnimeMediaType,
            duration: null,
            overrideTmdbId: null,
            seasonExplicit: false
        );

        result.Should().NotBeNull();
        result!.Value.match.SeasonNumber.Should().Be(2);
        result.Value.match.EpisodeNumber.Should().Be(1);
    }

    /// <summary>
    /// The provider hands back a group's episodes in curation order, not sequence, so the
    /// position each one declares is the only thing that says where it belongs. Reading the
    /// array as it arrives picks whichever episode happens to sit at that index.
    /// </summary>
    [Fact]
    public async Task GroupEpisodes_AreOrderedByTheirDeclaredPosition_NotArrayOrder()
    {
        SeedShowWithAGapInSeasonOne();

        Handler.WhenGet(
            "/search/tv",
            MockResponse.Json(
                HttpStatusCode.OK,
                $$"""
                {"page":1,"total_pages":1,"total_results":1,"results":[
                  {"id":{{ShowId}},"name":"{{ShowName}}","first_air_date":"2020-01-01"}
                ]}
                """
            )
        );
        Handler.WhenGet(
            $"/tv/{ShowId}/episode_groups",
            MockResponse.Json(
                HttpStatusCode.OK,
                """
                {"id":1,"results":[
                  {"id":"grp-shuffled","name":"Absolute Order","order":1,"type":2,"episode_count":3,"group_count":1}
                ]}
                """
            )
        );

        // Positions 0,1,2 are S01E07, S02E04, S01E02 — deliberately not the array order, so
        // asking for the second episode must yield S02E04 rather than the array's second slot.
        Handler.WhenGet(
            "/tv/episode_group/grp-shuffled",
            MockResponse.Json(
                HttpStatusCode.OK,
                """
                {"id":"grp-shuffled","name":"Absolute Order","type":2,"episode_count":3,"group_count":1,
                 "description":"","groups":[{"id":"g1","name":"Absolute Order","order":1,"locked":false,"episodes":[
                  {"id":102,"episode_number":2,"season_number":1,"name":"S01E02","overview":"","air_date":null,"order":2},
                  {"id":107,"episode_number":7,"season_number":1,"name":"S01E07","overview":"","air_date":null,"order":0},
                  {"id":204,"episode_number":4,"season_number":2,"name":"S02E04","overview":"","air_date":null,"order":1}
                 ]}]}
                """
            )
        );

        await using MediaContext context = new(_options);
        MediaIdentificationService service = BuildService(context);

        MovieFile parsed = new("/downloads/Ordering Probe - 2.mkv")
        {
            Title = ShowName,
            Season = 9,
            Episode = 2,
            IsSeries = true,
            IsSuccess = true,
        };

        (MovieOrEpisode match, string? imdbId)? result = await service.IdentifyAsync(
            parsed,
            MediaTypes.AnimeMediaType,
            duration: null,
            overrideTmdbId: null,
            seasonExplicit: false
        );

        result.Should().NotBeNull();
        result!.Value.match.SeasonNumber.Should().Be(2);
        result.Value.match.EpisodeNumber.Should().Be(4);
    }

    /// <summary>
    /// Modelled on a real absolute-order group: 25 specials, then 28 episodes, then 10 more
    /// that continue the same season's numbering. An absolute number counts the main run
    /// only, so the 29th episode is season 1 episode 29 — counting the specials first shifts
    /// it by 25 and lands on season 1 episode 4.
    /// </summary>
    [Fact]
    public async Task Specials_AreNotCounted_InAnAbsoluteRun()
    {
        // Both candidate answers exist locally, so whichever the resolver picks comes back as
        // a real row and the test reads the choice rather than a lookup failure. The parsed
        // season is one the library does not have, which is what sends this down the absolute
        // path in the first place.
        using (MediaContext seed = new(_options))
        {
            seed.Tvs.Add(new() { Id = ShowId, Title = ShowName });
            seed.Seasons.Add(
                new()
                {
                    Id = 1,
                    TvId = ShowId,
                    SeasonNumber = 1,
                }
            );
            seed.Episodes.Add(
                new()
                {
                    Id = 9004,
                    TvId = ShowId,
                    SeasonId = 1,
                    SeasonNumber = 1,
                    EpisodeNumber = 4,
                    Title = "Where counting the specials lands",
                }
            );
            seed.Episodes.Add(
                new()
                {
                    Id = 9029,
                    TvId = ShowId,
                    SeasonId = 1,
                    SeasonNumber = 1,
                    EpisodeNumber = 29,
                    Title = "Twenty-ninth of the main run",
                }
            );
            seed.SaveChanges();
        }

        Handler.WhenGet(
            "/search/tv",
            MockResponse.Json(
                HttpStatusCode.OK,
                $$"""
                {"page":1,"total_pages":1,"total_results":1,"results":[
                  {"id":{{ShowId}},"name":"{{ShowName}}","first_air_date":"2020-01-01"}
                ]}
                """
            )
        );
        Handler.WhenGet(
            $"/tv/{ShowId}/episode_groups",
            MockResponse.Json(
                HttpStatusCode.OK,
                """
                {"id":1,"results":[
                  {"id":"grp-abs","name":"Absolute Order","order":1,"type":2,"episode_count":63,"group_count":3}
                ]}
                """
            )
        );
        Handler.WhenGet(
            "/tv/episode_group/grp-abs",
            MockResponse.Json(
                HttpStatusCode.OK,
                $$"""
                {"id":"grp-abs","name":"Absolute Order","type":2,"episode_count":63,"group_count":3,
                 "description":"","groups":[
                  {"id":"g0","name":"Specials","order":0,"locked":false,"episodes":[{{SeasonRun(
                    0,
                    1,
                    25,
                    0
                )}}]},
                  {"id":"g1","name":"Season 01","order":1,"locked":false,"episodes":[{{SeasonRun(
                    1,
                    1,
                    28,
                    100
                )}}]},
                  {"id":"g2","name":"Season 02","order":2,"locked":false,"episodes":[{{SeasonRun(
                    1,
                    29,
                    10,
                    200
                )}}]}
                 ]}
                """
            )
        );

        await using MediaContext context = new(_options);
        MediaIdentificationService service = BuildService(context);

        MovieFile parsed = new("/downloads/Ordering Probe - 29.mkv")
        {
            Title = ShowName,
            Season = 2,
            Episode = 29,
            IsSeries = true,
            IsSuccess = true,
        };

        (MovieOrEpisode match, string? imdbId)? result = await service.IdentifyAsync(
            parsed,
            MediaTypes.AnimeMediaType,
            duration: null,
            overrideTmdbId: null,
            seasonExplicit: false
        );

        result.Should().NotBeNull();
        result!.Value.match.SeasonNumber.Should().Be(1);
        result.Value.match.EpisodeNumber.Should().Be(29);
    }

    private static string SeasonRun(int seasonNumber, int firstEpisode, int count, int idBase) =>
        string.Join(
            ",",
            Enumerable
                .Range(0, count)
                .Select(offset =>
                {
                    int episodeNumber = firstEpisode + offset;
                    int id = idBase + offset;
                    return $$"""
                    {"id":{{id}},"episode_number":{{episodeNumber}},"season_number":{{seasonNumber}},"name":"S{{seasonNumber}}E{{episodeNumber}}","overview":"","air_date":null,"order":{{offset}}}
                    """;
                })
        );

    [Fact]
    public async Task TheProviderOrdering_IsRequested_BeforeAnythingIsInferredLocally()
    {
        SeedShowWithAGapInSeasonOne();
        ScriptTmdb();

        await using MediaContext context = new(_options);
        MediaIdentificationService service = BuildService(context);

        MovieFile parsed = new("/downloads/Ordering Probe - 13.mkv")
        {
            Title = ShowName,
            Season = 1,
            Episode = 13,
            IsSeries = true,
            IsSuccess = true,
        };

        await service.IdentifyAsync(
            parsed,
            MediaTypes.AnimeMediaType,
            duration: null,
            overrideTmdbId: null,
            seasonExplicit: false
        );

        Handler.RequestCountFor("episode_group").Should().BeGreaterThan(0);
    }
}
