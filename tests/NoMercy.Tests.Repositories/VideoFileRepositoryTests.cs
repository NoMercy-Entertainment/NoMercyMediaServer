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
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class VideoFileRepositoryTests : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly VideoFileRepository _repository;

    public VideoFileRepositoryTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
        _repository = new(contextFactory: _factory);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsVideoFile_WhenIdExists()
    {
        VideoFile? result = await _repository.GetByIdAsync(id: SeedConstants.MovieVideoFile1Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: SeedConstants.MovieVideoFile1Id);
        result.Filename.Should().Be(expected: "Spirited.Away.2001.1080p.mkv");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();

        VideoFile? result = await _repository.GetByIdAsync(id: nonExistentId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenVideoFileExists()
    {
        bool result = await _repository.ExistsAsync(id: SeedConstants.MovieVideoFile1Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenVideoFileDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();

        bool result = await _repository.ExistsAsync(id: nonExistentId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetEncodedEpisodesForSeasonAsync_ReturnsEpisodesWithVideoFiles()
    {
        List<Episode> episodes = await _repository.GetEncodedEpisodesForSeasonAsync(seasonId: 3572);

        episodes.Should().HaveCount(expected: 2);
        episodes[index: 0].EpisodeNumber.Should().Be(expected: 1);
        episodes[index: 1].EpisodeNumber.Should().Be(expected: 2);
    }

    [Fact]
    public async Task GetEncodedEpisodesForSeasonAsync_ReturnsOnlyEpisodesWithVideoFiles()
    {
        await using MediaContext context = _factory.CreateDbContext();
        Season season = new()
        {
            Id = 9999,
            Title = "Season 2",
            SeasonNumber = 2,
            TvId = 1399,
        };
        context.Seasons.Add(entity: season);

        Episode episodeWithVideo = new()
        {
            Id = 99991,
            Title = "With Video",
            EpisodeNumber = 1,
            SeasonNumber = 2,
            TvId = 1399,
            SeasonId = 9999,
        };
        Episode episodeWithoutVideo = new()
        {
            Id = 99992,
            Title = "Without Video",
            EpisodeNumber = 2,
            SeasonNumber = 2,
            TvId = 1399,
            SeasonId = 9999,
        };
        context.Episodes.AddRange(entities: [episodeWithVideo, episodeWithoutVideo]);

        VideoFile videoFile = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "episode1.mkv",
            Folder = "/tv/test",
            HostFolder = "/tv/test",
            EpisodeId = 99991,
        };
        context.VideoFiles.Add(entity: videoFile);
        await context.SaveChangesAsync();

        List<Episode> results = await _repository.GetEncodedEpisodesForSeasonAsync(seasonId: 9999);

        results.Should().ContainSingle();
        results[index: 0].Id.Should().Be(expected: 99991);
    }

    [Fact]
    public async Task GetEncodedEpisodesForSeasonAsync_OrdersByEpisodeNumber()
    {
        await using MediaContext context = _factory.CreateDbContext();
        Season season = new()
        {
            Id = 8888,
            Title = "Season 3",
            SeasonNumber = 3,
            TvId = 1399,
        };
        context.Seasons.Add(entity: season);

        Episode episode3 = new()
        {
            Id = 88893,
            Title = "Episode 3",
            EpisodeNumber = 3,
            SeasonNumber = 3,
            TvId = 1399,
            SeasonId = 8888,
        };
        Episode episode1 = new()
        {
            Id = 88891,
            Title = "Episode 1",
            EpisodeNumber = 1,
            SeasonNumber = 3,
            TvId = 1399,
            SeasonId = 8888,
        };
        Episode episode2 = new()
        {
            Id = 88892,
            Title = "Episode 2",
            EpisodeNumber = 2,
            SeasonNumber = 3,
            TvId = 1399,
            SeasonId = 8888,
        };
        context.Episodes.AddRange(entities: [episode3, episode1, episode2]);

        foreach (Episode ep in new[] { episode1, episode2, episode3 })
        {
            context.VideoFiles.Add(
                entity: new()
                {
                    Id = Ulid.NewUlid(),
                    Filename = $"ep{ep.EpisodeNumber}.mkv",
                    Folder = "/tv/test",
                    HostFolder = "/tv/test",
                    EpisodeId = ep.Id,
                }
            );
        }
        await context.SaveChangesAsync();

        List<Episode> results = await _repository.GetEncodedEpisodesForSeasonAsync(seasonId: 8888);

        results.Should().HaveCount(expected: 3);
        results[index: 0].EpisodeNumber.Should().Be(expected: 1);
        results[index: 1].EpisodeNumber.Should().Be(expected: 2);
        results[index: 2].EpisodeNumber.Should().Be(expected: 3);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
