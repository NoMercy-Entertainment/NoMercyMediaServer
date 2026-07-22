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
using NoMercy.MediaProcessing.Images.Palettes;

namespace NoMercy.Tests.MediaProcessing.Palettes;

[Trait(name: "Category", value: "Unit")]
public class PaletteBackfillStateTests : IDisposable
{
    private readonly AppDbContext _db;

    public PaletteBackfillStateTests()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new(options: options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task IsComplete_returns_false_when_flag_not_set()
    {
        bool complete = await PaletteBackfillState.IsCompleteAsync(db: _db, ct: CancellationToken.None);
        complete.Should().BeFalse();
    }

    [Fact]
    public async Task SetComplete_then_IsComplete_returns_true()
    {
        await PaletteBackfillState.SetCompleteAsync(db: _db, ct: CancellationToken.None);
        bool complete = await PaletteBackfillState.IsCompleteAsync(db: _db, ct: CancellationToken.None);
        complete.Should().BeTrue();
    }

    [Fact]
    public async Task GetCursor_defaults_to_zero_when_not_set()
    {
        long cursor = await PaletteBackfillState.GetCursorAsync(
            db: _db,
            entityType: "movie",
            ct: CancellationToken.None
        );
        cursor.Should().Be(expected: 0L);
    }

    [Fact]
    public async Task SetCursor_then_GetCursor_round_trips()
    {
        await PaletteBackfillState.SetCursorAsync(db: _db, entityType: "movie", cursor: 42L, ct: CancellationToken.None);
        long cursor = await PaletteBackfillState.GetCursorAsync(
            db: _db,
            entityType: "movie",
            ct: CancellationToken.None
        );
        cursor.Should().Be(expected: 42L);
    }

    [Fact]
    public async Task SetCursor_overwrites_previous_value()
    {
        await PaletteBackfillState.SetCursorAsync(db: _db, entityType: "tv", cursor: 10L, ct: CancellationToken.None);
        await PaletteBackfillState.SetCursorAsync(db: _db, entityType: "tv", cursor: 99L, ct: CancellationToken.None);
        long cursor = await PaletteBackfillState.GetCursorAsync(db: _db, entityType: "tv", ct: CancellationToken.None);
        cursor.Should().Be(expected: 99L);
    }

    [Fact]
    public async Task Cursors_for_different_entity_types_are_independent()
    {
        await PaletteBackfillState.SetCursorAsync(db: _db, entityType: "movie", cursor: 100L, ct: CancellationToken.None);
        await PaletteBackfillState.SetCursorAsync(db: _db, entityType: "tv", cursor: 200L, ct: CancellationToken.None);

        long movieCursor = await PaletteBackfillState.GetCursorAsync(
            db: _db,
            entityType: "movie",
            ct: CancellationToken.None
        );
        long tvCursor = await PaletteBackfillState.GetCursorAsync(
            db: _db,
            entityType: "tv",
            ct: CancellationToken.None
        );

        movieCursor.Should().Be(expected: 100L);
        tvCursor.Should().Be(expected: 200L);
    }
}
