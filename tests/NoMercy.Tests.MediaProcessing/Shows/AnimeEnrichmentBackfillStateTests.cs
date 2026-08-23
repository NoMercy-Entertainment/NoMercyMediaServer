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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.MediaProcessing.Shows;

namespace NoMercy.Tests.MediaProcessing.Shows;

[Trait("Category", "Unit")]
public class AnimeEnrichmentBackfillStateTests : IDisposable
{
    private readonly AppDbContext _db;

    public AnimeEnrichmentBackfillStateTests()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task IsComplete_returns_false_when_flag_not_set()
    {
        bool complete = await AnimeEnrichmentBackfillState.IsCompleteAsync(
            _db,
            CancellationToken.None
        );
        complete.Should().BeFalse();
    }

    [Fact]
    public async Task SetComplete_then_IsComplete_returns_true()
    {
        await AnimeEnrichmentBackfillState.SetCompleteAsync(_db, CancellationToken.None);
        bool complete = await AnimeEnrichmentBackfillState.IsCompleteAsync(
            _db,
            CancellationToken.None
        );
        complete.Should().BeTrue();
    }

    [Fact]
    public async Task GetCursor_defaults_to_zero_when_not_set()
    {
        int cursor = await AnimeEnrichmentBackfillState.GetCursorAsync(
            _db,
            "movie",
            CancellationToken.None
        );
        cursor.Should().Be(0);
    }

    [Fact]
    public async Task SetCursor_then_GetCursor_round_trips()
    {
        await AnimeEnrichmentBackfillState.SetCursorAsync(_db, "movie", 42, CancellationToken.None);
        int cursor = await AnimeEnrichmentBackfillState.GetCursorAsync(
            _db,
            "movie",
            CancellationToken.None
        );
        cursor.Should().Be(42);
    }

    [Fact]
    public async Task SetCursor_overwrites_previous_value()
    {
        await AnimeEnrichmentBackfillState.SetCursorAsync(_db, "tv", 10, CancellationToken.None);
        await AnimeEnrichmentBackfillState.SetCursorAsync(_db, "tv", 99, CancellationToken.None);
        int cursor = await AnimeEnrichmentBackfillState.GetCursorAsync(
            _db,
            "tv",
            CancellationToken.None
        );
        cursor.Should().Be(99);
    }

    [Fact]
    public async Task Cursors_for_different_entity_types_are_independent()
    {
        await AnimeEnrichmentBackfillState.SetCursorAsync(
            _db,
            "movie",
            100,
            CancellationToken.None
        );
        await AnimeEnrichmentBackfillState.SetCursorAsync(_db, "tv", 200, CancellationToken.None);

        int movieCursor = await AnimeEnrichmentBackfillState.GetCursorAsync(
            _db,
            "movie",
            CancellationToken.None
        );
        int tvCursor = await AnimeEnrichmentBackfillState.GetCursorAsync(
            _db,
            "tv",
            CancellationToken.None
        );

        movieCursor.Should().Be(100);
        tvCursor.Should().Be(200);
    }
}
