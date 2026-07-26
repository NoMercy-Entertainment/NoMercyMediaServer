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
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// A paginated query that uses Skip/Take without an OrderBy returns rows in
/// SQLite's arbitrary physical order, so page 2 can repeat or drop rows from
/// page 1. These tests capture the generated SQL and assert the ordering
/// reaches the database — a regression that removes the OrderBy (or its stable
/// tiebreaker) stops emitting ORDER BY and fails here, at the SQL level, rather
/// than surfacing as an intermittent "duplicated card" bug in the client.
/// Empty tables still emit the query, so no seeding is required.
/// </summary>
[Trait("Category", "Unit")]
public class DeterministicOrderingTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly SqliteConnection _keepAlive;
    private readonly SqlCaptureInterceptor _interceptor = new();
    private readonly DbContextOptions<MediaContext> _options;

    public DeterministicOrderingTests()
    {
        string connectionString =
            $"DataSource={_dbName};Mode=Memory;Cache=Shared;Foreign Keys=True";
        _keepAlive = new(connectionString);
        _keepAlive.Open();
        _keepAlive.CreateFunction("normalize_search", (string? input) => input ?? string.Empty);

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connectionString,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(_interceptor, new SqliteNormalizeSearchInterceptor())
            .Options;

        using TestMediaContext init = new(_options);
        init.Database.EnsureCreated();
    }

    private string CapturedSqlContaining(string needle)
    {
        return string.Join(
            "\n",
            _interceptor.CapturedSql.Where(sql =>
                sql.Contains(needle, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    [Fact]
    public async Task HomeRepository_GetHome_OrdersPaginatedGenreQuery()
    {
        TestMediaContext context = new(_options);
        HomeRepository repository = new(context, new TestDbContextFactory(_options));
        _interceptor.Clear();

        await repository.GetHome(Guid.NewGuid(), "en", take: 10, page: 1);

        // The genre list query is the one that paginates (LIMIT/OFFSET); it must
        // carry an ORDER BY so page N is stable across requests.
        string genreQuery = CapturedSqlContaining("LIMIT");
        Assert.False(
            string.IsNullOrEmpty(genreQuery),
            "expected a paginated (LIMIT) query to be executed"
        );
        Assert.Contains("ORDER BY", genreQuery, StringComparison.OrdinalIgnoreCase);
    }

    private void AssertOrderedByColumnExists(string orderColumn)
    {
        // The main query must ORDER BY the real ordering column. The split-query
        // Include correlations order by the parent KEY, so keying the assertion
        // on the domain column (not just "ORDER BY") means a regression that
        // drops the primary ordering still fails here.
        bool ordered = _interceptor.CapturedSql.Any(sql =>
            sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase)
            && sql.Contains(orderColumn, StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(ordered, $"expected a query ordered by {orderColumn}");
    }

    [Fact]
    public async Task MusicRepository_GetLatestAlbums_OrdersByCreatedAt()
    {
        MusicRepository repository = new(new TestDbContextFactory(_options));
        _interceptor.Clear();

        await repository.GetLatestAlbums();

        AssertOrderedByColumnExists("CreatedAt");
    }

    [Fact]
    public async Task MusicRepository_GetLatestArtists_OrdersByCreatedAt()
    {
        MusicRepository repository = new(new TestDbContextFactory(_options));
        _interceptor.Clear();

        await repository.GetLatestArtists();

        AssertOrderedByColumnExists("CreatedAt");
    }

    /// <summary>
    /// Named-query tests only ever cover the queries someone remembered to name.
    /// The music start page went unordered because it is a second, parallel copy
    /// of card queries whose sequential originals were fixed — nothing pointed at
    /// the copy. This asserts the property instead of the instance: whatever SQL
    /// that page ends up running, anything carrying a LIMIT must carry an ORDER BY.
    /// </summary>
    [Fact]
    public async Task MusicRepository_GetMusicStartPage_OrdersEveryLimitedQuery()
    {
        MusicRepository repository = new(new TestDbContextFactory(_options));
        _interceptor.Clear();

        await repository.GetMusicStartPageAsync(Guid.NewGuid());

        List<string> limited = _interceptor
            .CapturedSql.Where(sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(limited);

        List<string> unordered = limited
            .Where(sql => !sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            unordered.Count == 0,
            $"{unordered.Count} start-page queries LIMIT rows without ordering them, so which "
                + $"rows come back is up to SQLite:\n\n{string.Join("\n\n", unordered)}"
        );
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }
}
