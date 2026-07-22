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

using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class ContentSegmentRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly ContentSegmentRepository _repository;

    public ContentSegmentRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(_context);
    }

    [Fact]
    public async Task CreateAsync_PersistsAndStampsTimestamps()
    {
        ContentSegment seg = Build(episodeId: 62085, type: ContentSegmentType.Intro);

        ContentSegment saved = await _repository.CreateAsync(seg);

        Assert.NotEqual(default, saved.CreatedAt);
        Assert.Equal(saved.CreatedAt, saved.UpdatedAt);

        ContentSegment? loaded = await _repository.GetByIdAsync(saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal(62085, loaded!.EpisodeId);
    }

    [Fact]
    public async Task GetForEpisodeAsync_ReturnsOnlyMatchingEpisode_OrderedByStart()
    {
        await _repository.CreateAsync(
            Build(episodeId: 62085, type: ContentSegmentType.Outro, start: 1800, end: 1830)
        );
        await _repository.CreateAsync(
            Build(episodeId: 62085, type: ContentSegmentType.Intro, start: 5, end: 60)
        );
        await _repository.CreateAsync(
            Build(episodeId: 62086, type: ContentSegmentType.Intro, start: 10, end: 40)
        );

        List<ContentSegment> forEpisode = await _repository.GetForEpisodeAsync(62085);

        Assert.Equal(2, forEpisode.Count);
        Assert.Equal(ContentSegmentType.Intro, forEpisode[0].SegmentType);
        Assert.Equal(ContentSegmentType.Outro, forEpisode[1].SegmentType);
    }

    [Fact]
    public async Task GetForMovieAsync_ReturnsOnlyMovieMatches()
    {
        await _repository.CreateAsync(
            Build(
                episodeId: null,
                movieId: 129,
                type: ContentSegmentType.Credits,
                start: 6000,
                end: 6300
            )
        );
        await _repository.CreateAsync(Build(episodeId: 62085, type: ContentSegmentType.Intro));

        List<ContentSegment> forMovie = await _repository.GetForMovieAsync(129);

        Assert.Single(forMovie);
        Assert.Equal(ContentSegmentType.Credits, forMovie[0].SegmentType);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        ContentSegment? updated = await _repository.UpdateAsync(Ulid.NewUlid(), _ => { });
        Assert.Null(updated);
    }

    [Fact]
    public async Task UpdateAsync_ChangesTimestamp()
    {
        ContentSegment original = await _repository.CreateAsync(Build(episodeId: 62085));
        await Task.Delay(5);

        ContentSegment? updated = await _repository.UpdateAsync(
            original.Id,
            s => s.Confidence = 0.5
        );

        Assert.NotNull(updated);
        Assert.Equal(0.5, updated!.Confidence);
        Assert.True(updated.UpdatedAt >= original.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_ExistingRow_Removes()
    {
        ContentSegment seg = await _repository.CreateAsync(Build(episodeId: 62085));

        bool removed = await _repository.DeleteAsync(seg.Id);

        Assert.True(removed);
        Assert.Null(await _repository.GetByIdAsync(seg.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        bool removed = await _repository.DeleteAsync(Ulid.NewUlid());
        Assert.False(removed);
    }

    [Fact]
    public async Task ReplaceDetectorSegmentsForEpisode_PreservesManualEdits()
    {
        // Manual edit stays.
        await _repository.CreateAsync(
            Build(
                episodeId: 62085,
                type: ContentSegmentType.Intro,
                start: 0,
                end: 30,
                source: "manual"
            )
        );
        // Old detector run — should be wiped and replaced.
        await _repository.CreateAsync(
            Build(
                episodeId: 62085,
                type: ContentSegmentType.Outro,
                start: 1700,
                end: 1730,
                source: "detector"
            )
        );

        List<ContentSegment> fresh =
        [
            Build(
                episodeId: 62085,
                type: ContentSegmentType.Intro,
                start: 10,
                end: 60,
                source: "detector"
            ),
            Build(
                episodeId: 62085,
                type: ContentSegmentType.Outro,
                start: 1800,
                end: 1830,
                source: "detector"
            ),
        ];

        await _repository.ReplaceDetectorSegmentsForEpisodeAsync(62085, fresh);

        List<ContentSegment> after = await _repository.GetForEpisodeAsync(62085);

        // Manual intro 0-30 must still be there.
        Assert.Contains(
            after,
            s => s is { Source: "manual", StartSeconds: 0, EndSeconds: 30 }
        );
        // New detector rows from the replacement.
        Assert.Contains(
            after,
            s =>
                s is { Source: "detector", SegmentType: ContentSegmentType.Intro, StartSeconds: 10 }
        );
        Assert.Contains(
            after,
            s =>
                s is { Source: "detector", SegmentType: ContentSegmentType.Outro, StartSeconds: 1800 }
        );
        // Old detector row should be gone.
        Assert.DoesNotContain(after, s => s is { Source: "detector", StartSeconds: 1700 });
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ContentSegment Build(
        int? episodeId = 100,
        int? movieId = null,
        ContentSegmentType type = ContentSegmentType.Intro,
        double start = 0,
        double end = 60,
        string source = "detector"
    ) =>
        new()
        {
            EpisodeId = episodeId,
            MovieId = movieId,
            SegmentType = type,
            StartSeconds = start,
            EndSeconds = end,
            Source = source,
            Confidence = 1.0,
        };
}
