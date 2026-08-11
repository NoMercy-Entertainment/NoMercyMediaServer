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
using NoMercy.Data.Plugins;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using NoMercy.Plugins.Abstractions;
using NoMercy.Tests.Repositories.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// Whether a show is still going out, as a plugin is told it.
/// <para>
/// The mapping is the whole point of the property: a plugin that had to read the
/// provider's own wording would carry a list of strings that goes stale the day the
/// provider renames one, in every plugin at once. These tests pin the translation so that
/// rename is a failure here instead.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class PluginLibraryQueryStatusTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly SqliteConnection _keepAlive;
    private readonly Ulid _libraryId = Ulid.NewUlid();

    public PluginLibraryQueryStatusTests()
    {
        _keepAlive = new($"DataSource={_dbName};Mode=Memory;Cache=Shared");
        _keepAlive.Open();
        _keepAlive.CreateFunction(
            "normalize_search",
            (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        using MediaContext context = CreateContext();
        context.Database.EnsureCreated();

        context.Libraries.Add(
            new Library
            {
                Id = _libraryId,
                Title = "Series",
                Type = "tv",
            }
        );
        context.SaveChanges();
    }

    [Theory]
    [InlineData("Returning Series", PluginShowStatus.Returning)]
    [InlineData("Ended", PluginShowStatus.Ended)]
    [InlineData("Canceled", PluginShowStatus.Canceled)]
    [InlineData("Cancelled", PluginShowStatus.Canceled)]
    [InlineData("Planned", PluginShowStatus.Planned)]
    [InlineData("In Production", PluginShowStatus.InProduction)]
    [InlineData("Pilot", PluginShowStatus.Pilot)]
    public async Task GetShowsAsync_TranslatesTheProvidersWording(
        string stored,
        PluginShowStatus expected
    )
    {
        await AddShow(1, stored);

        PluginLibraryShow show = await OnlyShow();

        Assert.Equal(expected, show.Status);
    }

    /// <summary>
    /// Casing and stray whitespace come from whoever wrote the row, not from a schema.
    /// </summary>
    [Theory]
    [InlineData("returning series")]
    [InlineData("  Returning Series  ")]
    [InlineData("RETURNING SERIES")]
    public async Task GetShowsAsync_IsNotFussyAboutHowTheWordWasWritten(string stored)
    {
        await AddShow(1, stored);

        Assert.Equal(PluginShowStatus.Returning, (await OnlyShow()).Status);
    }

    /// <summary>
    /// The one that matters most: an unrecognised or absent status must not read as
    /// finished. A plugin acting on "finished" stops working on the show, and a show
    /// nobody ended would then quietly stop being kept up to date.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Something TMDB Invented Last Tuesday")]
    public async Task GetShowsAsync_ReportsUnknownRatherThanGuessing(string? stored)
    {
        await AddShow(1, stored);

        PluginShowStatus status = (await OnlyShow()).Status;

        Assert.Equal(PluginShowStatus.Unknown, status);
        Assert.NotEqual(PluginShowStatus.Ended, status);
    }

    /// <summary>
    /// Everything the contract carried before the status is still carried, in the same
    /// places. The projection was rewritten to shape rows in memory to make room for the
    /// mapping, and that rewrite is exactly where a field goes missing.
    /// </summary>
    [Fact]
    public async Task GetShowsAsync_StillCarriesEverythingElse()
    {
        await AddShow(7, "Ended", title: "A Show", firstAired: new DateTime(2019, 4, 1), folder: "/A.Show.(2019)", episodes: 12, have: 5);

        PluginLibraryShow show = await OnlyShow();

        Assert.Equal(7, show.Id);
        Assert.Equal("A Show", show.Title);
        Assert.Equal(2019, show.Year);
        Assert.Equal(_libraryId.ToString(), show.LibraryId);
        Assert.Equal("/A.Show.(2019)", show.Folder);
        Assert.Equal(12, show.EpisodeCount);
        Assert.Equal(5, show.HaveEpisodeCount);
    }

    private async Task<PluginLibraryShow> OnlyShow()
    {
        PluginLibraryQuery query = new(CreateFactory());

        IReadOnlyList<PluginLibraryShow> shows = await query.GetShowsAsync(
            _libraryId.ToString()
        );

        return Assert.Single(shows);
    }

    private async Task AddShow(
        int id,
        string? status,
        string title = "Show",
        DateTime? firstAired = null,
        string? folder = "/Show",
        int episodes = 1,
        int have = 0
    )
    {
        await using MediaContext context = CreateContext();

        context.Tvs.Add(
            new Tv
            {
                Id = id,
                Title = title,
                TitleSort = title,
                LibraryId = _libraryId,
                Folder = folder,
                FirstAirDate = firstAired,
                NumberOfEpisodes = episodes,
                HaveEpisodes = have,
                Status = status,
            }
        );

        await context.SaveChangesAsync();
    }

    private MediaContext CreateContext()
    {
        SqliteConnection connection = new($"DataSource={_dbName};Mode=Memory;Cache=Shared");
        connection.Open();
        connection.CreateFunction(
            "normalize_search",
            (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        return new TestMediaContext(Options(connection));
    }

    private IDbContextFactory<MediaContext> CreateFactory() =>
        new TestDbContextFactory(
            new DbContextOptionsBuilder<MediaContext>()
                .UseSqlite(
                    $"DataSource={_dbName};Mode=Memory;Cache=Shared",
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                )
                .AddInterceptors(new SqliteNormalizeSearchInterceptor())
                .Options
        );

    private static DbContextOptions<MediaContext> Options(SqliteConnection connection) =>
        new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(new SqliteNormalizeSearchInterceptor())
            .Options;

    public void Dispose()
    {
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }
}
