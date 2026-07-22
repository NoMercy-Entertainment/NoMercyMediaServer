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

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class LanguageRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly SqliteConnection _connection;
    private readonly LanguageRepository _repository;

    public LanguageRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(context: _context);
        _connection = new(connectionString: "Data Source=:memory:");
    }

    [Fact]
    public async Task GetLanguagesAsync_ReturnsAllLanguages()
    {
        List<Language> result = await _repository.GetLanguagesAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(predicate: l => l.Iso6391 == "en");
    }

    [Fact]
    public async Task GetLanguagesAsync_IncludesEnglish()
    {
        List<Language> result = await _repository.GetLanguagesAsync();

        Language? english = result.FirstOrDefault(predicate: l => l.Iso6391 == "en");
        english.Should().NotBeNull();
        english!.EnglishName.Should().Be(expected: "English");
    }

    [Fact]
    public async Task GetLanguagesAsync_ReturnsMultipleLanguages()
    {
        _context.Languages.Add(
            entity: new()
            {
                Id = 2,
                Iso6391 = "fr",
                EnglishName = "French",
                Name = "Français",
            }
        );
        _context.Languages.Add(
            entity: new()
            {
                Id = 3,
                Iso6391 = "es",
                EnglishName = "Spanish",
                Name = "Español",
            }
        );
        await _context.SaveChangesAsync();

        List<Language> result = await _repository.GetLanguagesAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(expected: 3);
        result.Should().Contain(predicate: l => l.Iso6391 == "en");
        result.Should().Contain(predicate: l => l.Iso6391 == "fr");
        result.Should().Contain(predicate: l => l.Iso6391 == "es");
    }

    [Fact]
    public async Task GetCountriesAsync_ReturnsAllCountries()
    {
        Country country1 = new() { Id = 1, Iso31661 = "US" };
        Country country2 = new() { Id = 2, Iso31661 = "CA" };
        _context.Countries.AddRange(entities: [country1, country2]);
        await _context.SaveChangesAsync();

        List<Country> result = await _repository.GetCountriesAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(predicate: c => c.Iso31661 == "US");
        result.Should().Contain(predicate: c => c.Iso31661 == "CA");
    }

    [Fact]
    public async Task GetCountriesAsync_ReturnsEmptyWhenNoCountries()
    {
        await _context.Database.ExecuteSqlRawAsync(sql: "DELETE FROM Countries");

        List<Country> result = await _repository.GetCountriesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLanguagesAsync_WithFilter_ReturnsOnlyRequestedLanguages()
    {
        Language french = new()
        {
            Id = 2,
            Iso6391 = "fr",
            EnglishName = "French",
            Name = "Français",
        };
        Language spanish = new()
        {
            Id = 3,
            Iso6391 = "es",
            EnglishName = "Spanish",
            Name = "Español",
        };
        Language german = new()
        {
            Id = 4,
            Iso6391 = "de",
            EnglishName = "German",
            Name = "Deutsch",
        };
        _context.Languages.AddRange(entities: [french, spanish, german]);

        Ulid libraryId = SeedConstants.MovieLibraryId;
        _context.LanguageLibrary.Add(entity: new(languageId: french.Id, libraryId: libraryId));
        _context.LanguageLibrary.Add(entity: new(languageId: spanish.Id, libraryId: libraryId));
        await _context.SaveChangesAsync();

        List<LanguageLibrary> result = await _repository.GetLanguagesAsync(list: new[] { "fr", "es" });

        result.Should().HaveCount(expected: 2);
        result.Should().Contain(predicate: ll => ll.LanguageId == french.Id);
        result.Should().Contain(predicate: ll => ll.LanguageId == spanish.Id);
        result.Should().NotContain(predicate: ll => ll.LanguageId == german.Id);
    }

    [Fact]
    public async Task GetLanguagesAsync_WithFilter_ReturnsEmptyWhenNoMatch()
    {
        List<LanguageLibrary> result = await _repository.GetLanguagesAsync(list: new[] { "ja", "ko" });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLanguagesAsync_WithFilter_ReturnsSingleLanguage()
    {
        Language korean = new()
        {
            Id = 5,
            Iso6391 = "ko",
            EnglishName = "Korean",
            Name = "한국어",
        };
        _context.Languages.Add(entity: korean);

        Ulid libraryId = SeedConstants.MovieLibraryId;
        _context.LanguageLibrary.Add(entity: new(languageId: korean.Id, libraryId: libraryId));
        await _context.SaveChangesAsync();

        List<LanguageLibrary> result = await _repository.GetLanguagesAsync(list: new[] { "ko" });

        result.Should().ContainSingle();
        result[index: 0].LanguageId.Should().Be(expected: korean.Id);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
