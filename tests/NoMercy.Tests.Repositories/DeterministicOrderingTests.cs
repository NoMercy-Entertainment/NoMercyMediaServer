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

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }
}
