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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.Setup.Maintenance;

namespace NoMercy.Tests.Setup;

/// <summary>
/// Pins the behaviour that fixed music TitleSort not propagating: an algorithm
/// change must reach rows that already hold a (stale) value, not only null ones.
/// </summary>
[Trait("Category", "Unit")]
public class TitleSortBackfillTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public TitleSortBackfillTests()
    {
        _connection = new("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                _connection,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            .Options;

        using MediaContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private MediaContext CreateContext() => new(_options);

    [Fact]
    public async Task RunAsync_RecomputesDriftedValue_AndFillsNull()
    {
        Guid staleId = Guid.NewGuid();
        Guid nullId = Guid.NewGuid();

        await using (MediaContext ctx = CreateContext())
        {
            ctx.Artists.Add(
                new Artist
                {
                    Id = staleId,
                    Name = "The Beatles",
                    HostFolder = "a",
                    TitleSort = "value-from-an-older-algorithm",
                }
            );
            ctx.Artists.Add(
                new Artist
                {
                    Id = nullId,
                    Name = "A Perfect Circle",
                    HostFolder = "b",
                    TitleSort = null,
                }
            );
            await ctx.SaveChangesAsync();
        }

        await TitleSortBackfill.RunAsync(CreateContext, CancellationToken.None);

        await using (MediaContext ctx = CreateContext())
        {
            Artist drifted = await ctx.Artists.SingleAsync(a => a.Id == staleId);
            Artist wasNull = await ctx.Artists.SingleAsync(a => a.Id == nullId);

            // The stale value is replaced with the current algorithm's output
            // (leading article stripped, lower-cased), and the null is filled.
            drifted.TitleSort.Should().Be("The Beatles".TitleSort());
            drifted.TitleSort.Should().NotBe("value-from-an-older-algorithm");
            wasNull.TitleSort.Should().Be("A Perfect Circle".TitleSort());
        }
    }

    [Fact]
    public async Task RunAsync_LeavesUpToDateRowsUnchanged()
    {
        Guid id = Guid.NewGuid();
        string current = "The Beatles".TitleSort();

        await using (MediaContext ctx = CreateContext())
        {
            ctx.Artists.Add(
                new Artist
                {
                    Id = id,
                    Name = "The Beatles",
                    HostFolder = "a",
                    TitleSort = current,
                }
            );
            await ctx.SaveChangesAsync();
        }

        await TitleSortBackfill.RunAsync(CreateContext, CancellationToken.None);

        await using (MediaContext ctx = CreateContext())
        {
            Artist artist = await ctx.Artists.SingleAsync(a => a.Id == id);
            artist.TitleSort.Should().Be(current);
        }
    }
}
