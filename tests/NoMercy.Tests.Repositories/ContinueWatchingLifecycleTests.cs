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

using FlexLabs.EntityFrameworkCore.Upsert;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// Covers the full lifecycle around "remove from Continue Watching": the row must
/// survive the removal (RecommendationRepository still needs it), disappear from the
/// carousel while hidden, and come back automatically once the user resumes playback —
/// mirroring the exact FlexLabs upsert shape used by VideoPlaybackService.SetTime and
/// VideoHub.Playback.SetTime.
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class ContinueWatchingLifecycleTests : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly RecommendationRepository _recommendationRepository;

    public ContinueWatchingLifecycleTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
        _recommendationRepository = new(contextFactory: _factory);
    }

    // HomeRepository.GetContinueWatchingAsync reads from the MediaContext handed to its
    // constructor rather than the factory — build a scoped repository per call so each
    // assertion sees the latest committed state instead of a stale tracked snapshot.
    private async Task<HashSet<UserData>> GetContinueWatchingAsync()
    {
        await using MediaContext context = await _factory.CreateDbContextAsync();
        HomeRepository repository = new(context: context, contextFactory: _factory);
        return await repository.GetContinueWatchingAsync(userId: SeedConstants.UserId, language: "en", country: "US");
    }

    [Fact]
    public async Task RecordingNewProgress_ClearsHiddenFlag_AndItemReappearsInContinueWatching()
    {
        // Arrange: simulate the user having removed movie 129 from Continue Watching —
        // UserDataController.RemoveContinue hides every UserData row for the movie id,
        // not just one video file, so both seeded rows must be hidden here too.
        await using (MediaContext context = await _factory.CreateDbContextAsync())
        {
            List<UserData> rows = await context
                .UserData.Where(predicate: ud => ud.MovieId == 129)
                .ToListAsync();
            foreach (UserData row in rows)
                row.RemovedFromContinueWatching = true;
            await context.SaveChangesAsync();
        }

        HashSet<UserData> beforeResume = await GetContinueWatchingAsync();
        beforeResume.Should().NotContain(predicate: ud => ud.MovieId == 129);

        // Act: replicate the exact upsert VideoPlaybackService.SetTime / VideoHub.Playback.SetTime
        // run every time new playback progress is recorded for the item.
        await using (MediaContext context = await _factory.CreateDbContextAsync())
        {
            UserData incoming = new()
            {
                UserId = SeedConstants.UserId,
                Type = "movie",
                Time = 900,
                VideoFileId = SeedConstants.MovieVideoFile1Id,
                MovieId = 129,
            };

            await context
                .UserData.Upsert(entity: incoming)
                .On(match: x => new
                {
                    x.VideoFileId,
                    x.UserId,
                    x.MovieId,
                })
                .WhenMatched(
                    updater: (uds, udi) =>
                        new()
                        {
                            Id = uds.Id,
                            Type = udi.Type,
                            MovieId = udi.MovieId,
                            TvId = udi.TvId,
                            CollectionId = udi.CollectionId,
                            SpecialId = udi.SpecialId,
                            Time = udi.Time,
                            Audio = udi.Audio,
                            Subtitle = udi.Subtitle,
                            SubtitleType = udi.SubtitleType,
                            LastPlayedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            RemovedFromContinueWatching = false,
                        }
                )
                .RunAsync();
        }

        // Assert: the flag is cleared and the item is back in the carousel.
        await using (MediaContext context = await _factory.CreateDbContextAsync())
        {
            UserData updated = await context.UserData.FirstAsync(predicate: ud =>
                ud.MovieId == 129 && ud.VideoFileId == SeedConstants.MovieVideoFile1Id
            );
            updated.RemovedFromContinueWatching.Should().BeFalse();
            updated.Time.Should().Be(expected: 900);
        }

        HashSet<UserData> afterResume = await GetContinueWatchingAsync();
        afterResume.Should().Contain(predicate: ud => ud.MovieId == 129);
    }

    [Fact]
    public async Task HiddenRow_StillCountsTowardMovieAffinity()
    {
        // Arrange: hide movie 129 from Continue Watching and give it a rating, the way a
        // user who watched-then-removed-then-rated an item would leave the data.
        await using (MediaContext context = await _factory.CreateDbContextAsync())
        {
            List<UserData> rows = await context
                .UserData.Where(predicate: ud => ud.MovieId == 129)
                .ToListAsync();
            foreach (UserData row in rows)
            {
                row.RemovedFromContinueWatching = true;
                row.Rating = 8;
            }
            await context.SaveChangesAsync();
        }

        // Act
        List<UserAffinitySourceDto> affinity =
            await _recommendationRepository.GetUserMovieAffinityDataAsync(userId: SeedConstants.UserId);

        // Assert: RecommendationRepository never filters on RemovedFromContinueWatching —
        // the hidden row's signal must still feed the affinity model.
        UserAffinitySourceDto? movieAffinity = affinity.FirstOrDefault(predicate: a => a.ItemId == 129);
        movieAffinity.Should().NotBeNull();
        movieAffinity!.Rating.Should().Be(expected: 8);
        movieAffinity.TimeWatched.Should().Be(expected: 3600);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
