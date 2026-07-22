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
[Trait(name: "Category", value: "Unit")]
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
        _keepAlive = new(connectionString: connectionString);
        _keepAlive.Open();
        _keepAlive.CreateFunction(name: "normalize_search", function: (string? input) => input ?? string.Empty);

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connectionString: connectionString,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: [_interceptor, new SqliteNormalizeSearchInterceptor()])
            .Options;

        using TestMediaContext init = new(options: _options);
        init.Database.EnsureCreated();
    }

    private string CapturedSqlContaining(string needle)
    {
        return string.Join(
            separator: "\n",
            values: _interceptor.CapturedSql.Where(predicate: sql =>
                sql.Contains(value: needle, comparisonType: StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    [Fact]
    public async Task HomeRepository_GetHome_OrdersPaginatedGenreQuery()
    {
        TestMediaContext context = new(options: _options);
        HomeRepository repository = new(context: context, contextFactory: new TestDbContextFactory(options: _options));
        _interceptor.Clear();

        await repository.GetHome(userId: Guid.NewGuid(), language: "en", take: 10, page: 1);

        // The genre list query is the one that paginates (LIMIT/OFFSET); it must
        // carry an ORDER BY so page N is stable across requests.
        string genreQuery = CapturedSqlContaining(needle: "LIMIT");
        Assert.False(
            condition: string.IsNullOrEmpty(value: genreQuery),
            userMessage: "expected a paginated (LIMIT) query to be executed"
        );
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: genreQuery, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private void AssertOrderedByColumnExists(string orderColumn)
    {
        // The main query must ORDER BY the real ordering column. The split-query
        // Include correlations order by the parent KEY, so keying the assertion
        // on the domain column (not just "ORDER BY") means a regression that
        // drops the primary ordering still fails here.
        bool ordered = _interceptor.CapturedSql.Any(predicate: sql =>
            sql.Contains(value: "ORDER BY", comparisonType: StringComparison.OrdinalIgnoreCase)
            && sql.Contains(value: orderColumn, comparisonType: StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(condition: ordered, userMessage: $"expected a query ordered by {orderColumn}");
    }

    [Fact]
    public async Task MusicRepository_GetLatestAlbums_OrdersByCreatedAt()
    {
        MusicRepository repository = new(contextFactory: new TestDbContextFactory(options: _options));
        _interceptor.Clear();

        await repository.GetLatestAlbums();

        AssertOrderedByColumnExists(orderColumn: "CreatedAt");
    }

    [Fact]
    public async Task MusicRepository_GetLatestArtists_OrdersByCreatedAt()
    {
        MusicRepository repository = new(contextFactory: new TestDbContextFactory(options: _options));
        _interceptor.Clear();

        await repository.GetLatestArtists();

        AssertOrderedByColumnExists(orderColumn: "CreatedAt");
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
        GC.SuppressFinalize(obj: this);
    }
}
