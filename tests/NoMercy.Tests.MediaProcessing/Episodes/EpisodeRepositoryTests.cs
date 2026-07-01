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
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Episodes;

namespace NoMercy.Tests.MediaProcessing.Episodes;

public class EpisodeRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public EpisodeRepositoryTests()
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

    [Fact]
    public async Task StoreEpisodes_inserts_new_episode_with_all_fields()
    {
        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        int tvId = 1;
        int seasonId = 1;
        int episodeId = 101;

        Episode episode = new()
        {
            Id = episodeId,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            AirDate = new DateTime(2023, 01, 15),
            Overview = "The first episode introduces the main characters.",
            ProductionCode = "E001",
            Still = "/still_path.jpg",
            TvId = tvId,
            SeasonId = seasonId,
        };

        await repo.StoreEpisodes(new[] { episode });

        using MediaContext verify = new(_options);
        Episode? stored = await verify.Episodes.FirstOrDefaultAsync(e => e.Id == episodeId);

        stored.Should().NotBeNull();
        stored!.Title.Should().Be("Pilot");
        stored.EpisodeNumber.Should().Be(1);
        stored.SeasonNumber.Should().Be(1);
        stored.AirDate.Should().Be(new DateTime(2023, 01, 15));
        stored.Overview.Should().Contain("first episode");
        stored.ProductionCode.Should().Be("E001");
        stored.Still.Should().Be("/still_path.jpg");
        stored.TvId.Should().Be(tvId);
        stored.SeasonId.Should().Be(seasonId);
    }

    [Fact]
    public async Task StoreEpisodes_updates_existing_episode_on_upsert()
    {
        using MediaContext seed = new(_options);
        int episodeId = 101;
        Episode original = new()
        {
            Id = episodeId,
            Title = "Old Title",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            AirDate = new DateTime(2023, 01, 01),
            Overview = "Old overview",
            ProductionCode = "OLD",
            Still = "/old.jpg",
            TvId = 1,
            SeasonId = 1,
        };
        seed.Episodes.Add(original);
        await seed.SaveChangesAsync();

        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        Episode updated = new()
        {
            Id = episodeId,
            Title = "Updated Title",
            EpisodeNumber = 2,
            SeasonNumber = 1,
            AirDate = new DateTime(2023, 01, 22),
            Overview = "Updated overview with new content",
            ProductionCode = "NEW",
            Still = "/new.jpg",
            TvId = 1,
            SeasonId = 1,
        };

        await repo.StoreEpisodes(new[] { updated });

        using MediaContext verify = new(_options);
        Episode? result = await verify.Episodes.FirstOrDefaultAsync(e => e.Id == episodeId);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated Title");
        result.EpisodeNumber.Should().Be(2);
        result.AirDate.Should().Be(new DateTime(2023, 01, 22));
        result.ProductionCode.Should().Be("NEW");
    }

    [Fact]
    public async Task StoreEpisodes_stores_multiple_episodes_for_season()
    {
        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        int tvId = 1;
        int seasonId = 1;

        Episode[] episodes = new[]
        {
            new Episode
            {
                Id = 101,
                Title = "Episode 1",
                EpisodeNumber = 1,
                SeasonNumber = 1,
                AirDate = new DateTime(2023, 01, 15),
                Overview = "First episode",
                ProductionCode = "E001",
                Still = "/still1.jpg",
                TvId = tvId,
                SeasonId = seasonId,
            },
            new Episode
            {
                Id = 102,
                Title = "Episode 2",
                EpisodeNumber = 2,
                SeasonNumber = 1,
                AirDate = new DateTime(2023, 01, 22),
                Overview = "Second episode",
                ProductionCode = "E002",
                Still = "/still2.jpg",
                TvId = tvId,
                SeasonId = seasonId,
            },
            new Episode
            {
                Id = 103,
                Title = "Episode 3",
                EpisodeNumber = 3,
                SeasonNumber = 1,
                AirDate = new DateTime(2023, 01, 29),
                Overview = "Third episode",
                ProductionCode = "E003",
                Still = "/still3.jpg",
                TvId = tvId,
                SeasonId = seasonId,
            },
        };

        await repo.StoreEpisodes(episodes);

        using MediaContext verify = new(_options);
        List<Episode> stored = await verify.Episodes
            .Where(e => e.SeasonId == seasonId)
            .OrderBy(e => e.EpisodeNumber)
            .ToListAsync();

        stored.Should().HaveCount(3);
        stored.Should().Satisfy(
            e => e.EpisodeNumber == 1 && e.Title == "Episode 1",
            e => e.EpisodeNumber == 2 && e.Title == "Episode 2",
            e => e.EpisodeNumber == 3 && e.Title == "Episode 3"
        );
    }

    [Fact]
    public async Task StoreEpisodeTranslations_inserts_translations_only_for_existing_episodes()
    {
        using MediaContext seed = new(_options);
        int episodeId = 101;
        Episode episode = new()
        {
            Id = episodeId,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            AirDate = null,
            Overview = "Overview",
            ProductionCode = null,
            Still = null,
            TvId = 1,
            SeasonId = 1,
        };
        seed.Episodes.Add(episode);
        await seed.SaveChangesAsync();

        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        List<Translation> translations = new()
        {
            new Translation
            {
                Iso31661 = "US",
                Iso6391 = "en",
                Title = "Pilot",
                Overview = "The pilot episode",
                Name = "English",
                EnglishName = "English",
                EpisodeId = episodeId,
            },
            new Translation
            {
                Iso31661 = "FR",
                Iso6391 = "fr",
                Title = "Pilote",
                Overview = "L'épisode pilote",
                Name = "French",
                EnglishName = "French",
                EpisodeId = 999,
            },
        };

        await repo.StoreEpisodeTranslations(translations);

        using MediaContext verify = new(_options);
        List<Translation> stored = await verify.Translations
            .Where(t => t.EpisodeId == episodeId || t.EpisodeId == 999)
            .ToListAsync();

        stored.Should().HaveCount(1);
        stored.Single().Iso6391.Should().Be("en");
    }

    [Fact]
    public async Task StoreEpisodeTranslations_upserts_existing_translations()
    {
        using MediaContext seed = new(_options);
        int episodeId = 101;
        Episode episode = new()
        {
            Id = episodeId,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            AirDate = null,
            Overview = "Overview",
            ProductionCode = null,
            Still = null,
            TvId = 1,
            SeasonId = 1,
        };
        seed.Episodes.Add(episode);

        Translation existing = new()
        {
            Iso31661 = "US",
            Iso6391 = "en",
            Title = "Old Title",
            Overview = "Old overview",
            Name = "English",
            EnglishName = "English",
            EpisodeId = episodeId,
        };
        seed.Translations.Add(existing);
        await seed.SaveChangesAsync();

        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        List<Translation> translations = new()
        {
            new Translation
            {
                Iso31661 = "US",
                Iso6391 = "en",
                Title = "Updated Title",
                Overview = "Updated overview",
                Name = "English",
                EnglishName = "English",
                EpisodeId = episodeId,
            },
        };

        await repo.StoreEpisodeTranslations(translations);

        using MediaContext verify = new(_options);
        Translation? stored = await verify.Translations.FirstOrDefaultAsync(
            t => t.EpisodeId == episodeId && t.Iso6391 == "en"
        );

        stored.Should().NotBeNull();
        stored!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task StoreEpisodeImages_inserts_images_only_for_existing_episodes()
    {
        using MediaContext seed = new(_options);
        int episodeId = 101;
        Episode episode = new()
        {
            Id = episodeId,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            AirDate = null,
            Overview = "Overview",
            ProductionCode = null,
            Still = null,
            TvId = 1,
            SeasonId = 1,
        };
        seed.Episodes.Add(episode);
        await seed.SaveChangesAsync();

        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        Image[] images = new[]
        {
            new Image
            {
                FilePath = "/still1.jpg",
                AspectRatio = 1.778,
                Height = 1080,
                Width = 1920,
                VoteAverage = 8.0,
                VoteCount = 42,
                Iso6391 = "en",
                Type = "still",
                Site = "https://image.tmdb.org/t/p/",
                EpisodeId = episodeId,
            },
            new Image
            {
                FilePath = "/still2.jpg",
                AspectRatio = 1.778,
                Height = 1080,
                Width = 1920,
                VoteAverage = 7.5,
                VoteCount = 10,
                Iso6391 = "en",
                Type = "still",
                Site = "https://image.tmdb.org/t/p/",
                EpisodeId = 999,
            },
        };

        await repo.StoreEpisodeImages(images);

        using MediaContext verify = new(_options);
        List<Image> stored = await verify.Images
            .Where(i => i.EpisodeId == episodeId || i.EpisodeId == 999)
            .ToListAsync();

        stored.Should().HaveCount(1);
        stored.Single().FilePath.Should().Be("/still1.jpg");
    }

    [Fact]
    public async Task StoreEpisodeImages_upserts_existing_images()
    {
        using MediaContext seed = new(_options);
        int episodeId = 101;
        Episode episode = new()
        {
            Id = episodeId,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            AirDate = null,
            Overview = "Overview",
            ProductionCode = null,
            Still = null,
            TvId = 1,
            SeasonId = 1,
        };
        seed.Episodes.Add(episode);

        Image existing = new()
        {
            FilePath = "/still.jpg",
            AspectRatio = 1.778,
            Height = 1080,
            Width = 1920,
            VoteAverage = 5.0,
            VoteCount = 5,
            Iso6391 = "en",
            Type = "still",
            Site = "https://image.tmdb.org/t/p/",
            EpisodeId = episodeId,
        };
        seed.Images.Add(existing);
        await seed.SaveChangesAsync();

        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        Image[] images = new[]
        {
            new Image
            {
                FilePath = "/still.jpg",
                AspectRatio = 1.778,
                Height = 1080,
                Width = 1920,
                VoteAverage = 9.0,
                VoteCount = 100,
                Iso6391 = "en",
                Type = "still",
                Site = "https://image.tmdb.org/t/p/",
                EpisodeId = episodeId,
            },
        };

        await repo.StoreEpisodeImages(images);

        using MediaContext verify = new(_options);
        Image? stored = await verify.Images.FirstOrDefaultAsync(
            i => i.FilePath == "/still.jpg" && i.EpisodeId == episodeId
        );

        stored.Should().NotBeNull();
        stored!.VoteAverage.Should().Be(9.0);
        stored.VoteCount.Should().Be(100);
    }

    [Fact]
    public async Task StoreEpisodeImages_handles_empty_image_list()
    {
        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        await repo.StoreEpisodeImages(Array.Empty<Image>());

        using MediaContext verify = new(_options);
        int count = await verify.Images.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task StoreEpisodeImages_stores_multiple_images_for_episode()
    {
        using MediaContext seed = new(_options);
        int episodeId = 101;
        Episode episode = new()
        {
            Id = episodeId,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            AirDate = null,
            Overview = "Overview",
            ProductionCode = null,
            Still = null,
            TvId = 1,
            SeasonId = 1,
        };
        seed.Episodes.Add(episode);
        await seed.SaveChangesAsync();

        using MediaContext context = new(_options);
        EpisodeRepository repo = new(context);

        Image[] images = new[]
        {
            new Image
            {
                FilePath = "/still1.jpg",
                AspectRatio = 1.778,
                Height = 1080,
                Width = 1920,
                VoteAverage = 8.0,
                VoteCount = 42,
                Iso6391 = "en",
                Type = "still",
                Site = "https://image.tmdb.org/t/p/",
                EpisodeId = episodeId,
            },
            new Image
            {
                FilePath = "/still2.jpg",
                AspectRatio = 1.778,
                Height = 1080,
                Width = 1920,
                VoteAverage = 7.5,
                VoteCount = 30,
                Iso6391 = "en",
                Type = "still",
                Site = "https://image.tmdb.org/t/p/",
                EpisodeId = episodeId,
            },
        };

        await repo.StoreEpisodeImages(images);

        using MediaContext verify = new(_options);
        List<Image> stored = await verify.Images
            .Where(i => i.EpisodeId == episodeId)
            .OrderBy(i => i.FilePath)
            .ToListAsync();

        stored.Should().HaveCount(2);
        stored.Should().Satisfy(
            i => i.FilePath == "/still1.jpg" && i.VoteAverage == 8.0,
            i => i.FilePath == "/still2.jpg" && i.VoteAverage == 7.5
        );
    }
}
