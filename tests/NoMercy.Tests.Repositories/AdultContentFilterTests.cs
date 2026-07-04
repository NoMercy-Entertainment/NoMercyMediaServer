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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.People;
using NoMercy.NmSystem.Configuration;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Unit")]
public class AdultContentFilterTests : IDisposable
{
    private const int AdultMovieId = 9_999_001;

    private readonly bool? _originalAllowAdult = RuntimeServerSettings.Current.AllowAdultContent;
    private readonly MediaContext _context;

    public AdultContentFilterTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
    }

    public void Dispose()
    {
        RuntimeServerSettings.Current.AllowAdultContent = _originalAllowAdult;
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ShowAdultContent_TreatsNullAndFalseAsHidden(bool? configured, bool expected)
    {
        RuntimeServerSettings.Current.AllowAdultContent = configured;

        Assert.Equal(expected, RuntimeServerSettings.Current.ShowAdultContent);
    }

    [Fact]
    public async Task MovieQueryFilter_HidesAdultRows_WhenUnconfigured()
    {
        RuntimeServerSettings.Current.AllowAdultContent = null; // the shipped default
        AddAdultMovie();

        List<Movie> visible = await _context.Movies.ToListAsync();

        Assert.DoesNotContain(visible, movie => movie.Id == AdultMovieId);
        Assert.Contains(visible, movie => movie.Id == 129); // seeded non-adult
    }

    [Fact]
    public async Task MovieQueryFilter_ShowsAdultRows_WhenAllowed()
    {
        RuntimeServerSettings.Current.AllowAdultContent = true;
        AddAdultMovie();

        List<Movie> visible = await _context.Movies.ToListAsync();

        Assert.Contains(visible, movie => movie.Id == AdultMovieId);
    }

    [Fact]
    public async Task MovieQueryFilter_IsBypassedByIgnoreQueryFilters()
    {
        RuntimeServerSettings.Current.AllowAdultContent = false;
        AddAdultMovie();

        Movie? viaFilter = await _context.Movies.FirstOrDefaultAsync(movie =>
            movie.Id == AdultMovieId
        );
        Movie? viaBypass = await _context
            .Movies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(movie => movie.Id == AdultMovieId);

        Assert.Null(viaFilter);
        Assert.NotNull(viaBypass);
    }

    [Fact]
    public async Task PersonQueryFilter_HidesAdultPeople_WhenNotAllowed()
    {
        RuntimeServerSettings.Current.AllowAdultContent = false;
        _context.People.Add(
            new Person
            {
                Id = 11,
                Name = "Clean Actor",
                Adult = false,
            }
        );
        _context.People.Add(
            new Person
            {
                Id = 12,
                Name = "Adult Performer",
                Adult = true,
            }
        );
        await _context.SaveChangesAsync();

        List<Person> visible = await _context.People.ToListAsync();

        Assert.Contains(visible, person => person.Id == 11);
        Assert.DoesNotContain(visible, person => person.Id == 12);
    }

    private void AddAdultMovie()
    {
        _context.Movies.Add(
            new Movie
            {
                Id = AdultMovieId,
                Title = "Explicit Title",
                TitleSort = "explicit title",
                Adult = true,
                LibraryId = SeedConstants.MovieLibraryId,
            }
        );
        _context.SaveChanges();
    }
}
