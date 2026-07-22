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
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Tests.Database;

public class InboxItemPersistenceTests : IDisposable
{
    private readonly MediaContext _context;

    public InboxItemPersistenceTests()
    {
        DbContextOptionsBuilder<MediaContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");

        _context = new(options: optionsBuilder.Options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task InboxItem_RoundTrips_CandidatesJson()
    {
        Ulid id = Ulid.NewUlid();
        InboxItem item = new()
        {
            Id = id,
            SourcePath = "Inbox/The Matrix (1999)/movie.mkv",
            DriverId = Ulid.NewUlid(),
            DetectedType = "movie",
            Confidence = "high",
            Status = "NeedsReview",
            Candidates =
            [
                new()
                {
                    Provider = "tmdb",
                    ExternalId = "603",
                    Title = "The Matrix",
                    Year = 1999,
                    Score = 0.98,
                },
            ],
        };

        _context.InboxItems.Add(entity: item);
        await _context.SaveChangesAsync();

        InboxItem loaded = await _context.InboxItems.SingleAsync(predicate: i => i.Id == id);

        Assert.Single(collection: loaded.Candidates);
        Assert.Equal(expected: "The Matrix", actual: loaded.Candidates[0].Title);
        Assert.Equal(expected: 1999, actual: loaded.Candidates[0].Year);
    }
}
