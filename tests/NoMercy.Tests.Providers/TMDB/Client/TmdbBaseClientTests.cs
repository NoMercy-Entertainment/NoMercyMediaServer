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

using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Genres;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Tests for TmdbBaseClient functionality
/// Tests the base HTTP client behavior and common functionality
/// </summary>
[Collection(name: "TmdbApi")]
public class TmdbBaseClientTests : TmdbTestBase
{
    private class TestableBaseClient : TmdbBaseClient
    {
        public TestableBaseClient() { }

        public TestableBaseClient(int id, string language = "en-US")
            : base(id: id, language: language) { }

        public new Task<T?> Get<T>(
            string url,
            Dictionary<string, string?>? query = null,
            bool? priority = false,
            bool skipCache = false
        )
            where T : class
        {
            return base.Get<T>(url: url, query: query, priority: priority, skipCache: skipCache);
        }

        public new Task<List<T>?> Paginated<T>(string url, int limit)
            where T : class
        {
            return base.Paginated<T>(url: url, limit: limit);
        }
    }

    [Fact]
    public void Constructor_Default_CreatesClientWithZeroId()
    {
        // Arrange & Act
        using TestableBaseClient client = new();

        // Assert
        client.Id.Should().Be(expected: 0);
    }

    [Fact]
    public void Constructor_WithIdAndLanguage_SetsPropertiesCorrectly()
    {
        // Arrange
        const int expectedId = 12345;
        const string language = "fr-FR";

        // Act
        using TestableBaseClient client = new(id: expectedId, language: language);

        // Assert
        client.Id.Should().Be(expected: expectedId);
    }

    [Theory]
    [InlineData(data: "en-US")]
    [InlineData(data: "fr-FR")]
    [InlineData(data: "es-ES")]
    [InlineData(data: "de-DE")]
    [InlineData(data: "ja-JP")]
    public void Constructor_WithDifferentLanguages_CreatesClientSuccessfully(string language)
    {
        // Arrange & Act
        using TestableBaseClient client = new(id: ValidMovieId, language: language);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public void Dispose_CalledOnce_DisposesCorrectly()
    {
        // Arrange
        TestableBaseClient client = new();

        // Act & Assert
        Action act = () => client.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        TestableBaseClient client = new();

        // Act & Assert
        client.Dispose();
        Action act = () => client.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNegativeId_SetsIdCorrectly()
    {
        // Arrange
        const int negativeId = -1;

        // Act
        using TestableBaseClient client = new(id: negativeId);

        // Assert
        client.Id.Should().Be(expected: negativeId);
    }

    [Fact]
    public void Constructor_WithMaxIntId_SetsIdCorrectly()
    {
        // Arrange
        const int maxId = int.MaxValue;

        // Act
        using TestableBaseClient client = new(id: maxId);

        // Assert
        client.Id.Should().Be(expected: maxId);
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: null)]
    [InlineData(data: "invalid-language")]
    public void Constructor_WithInvalidLanguage_CreatesClientSuccessfully(string? language)
    {
        // Arrange & Act
        using TestableBaseClient client = new(id: ValidMovieId, language: language!);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public void MultipleClients_CreatedSimultaneously_WorkIndependently()
    {
        // Arrange
        const int id1 = 100;
        const int id2 = 200;
        const string lang2 = "fr-FR";

        // Act
        using TestableBaseClient client1 = new(id: id1);
        using TestableBaseClient client2 = new(id: id2, language: lang2);

        // Assert
        client1.Id.Should().Be(expected: id1);
        client2.Id.Should().Be(expected: id2);
        client1.Should().NotBeSameAs(unexpected: client2);
    }

    [Fact]
    public void Constructor_WithEmptyLanguage_CreatesClientSuccessfully()
    {
        // Arrange
        const string emptyLanguage = "";

        // Act
        using TestableBaseClient client = new(id: ValidMovieId, language: emptyLanguage);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public void Client_AfterDispose_PropertiesStillAccessible()
    {
        // Arrange
        TestableBaseClient client = new(id: ValidMovieId);
        int originalId = client.Id;

        // Act
        client.Dispose();

        // Assert
        client.Id.Should().Be(expected: originalId);
    }

    [Fact]
    public async Task Get_PreservesCallerSuppliedLanguage_WhenPriorityFalse()
    {
        // Regression for the genre-i18n bug: Get<T> used to unconditionally
        // overwrite query["language"] with "" whenever priority wasn't true,
        // silently dropping a caller-supplied language (e.g. TmdbMovieClient
        // /TmdbTvClient Genres(language)). The merge is the first statement
        // in Get<T> and runs synchronously before any I/O, so the caller's
        // dictionary is already mutated the instant the call returns —
        // no need to wait on the (real) network response to assert on it.
        using TestableBaseClient client = new(id: ValidMovieId, language: "en-US");
        Dictionary<string, string?> query = new() { [key: "language"] = "nl" };

        Task<TmdbGenreMovies?> task = client.Get<TmdbGenreMovies>(
            url: "genre/movie/list",
            query: query,
            priority: false
        );

        query[key: "language"].Should().Be(expected: "nl");

        client.Dispose();
        try
        {
            await task;
        }
        catch
        {
            // Dispose/network noise from the in-flight call is irrelevant —
            // this test only asserts on the query-merge side effect above.
        }
    }
}
