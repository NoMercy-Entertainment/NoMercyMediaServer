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
[Collection("TmdbApi")]
public class TmdbBaseClientTests : TmdbTestBase
{
    private class TestableBaseClient : TmdbBaseClient
    {
        public TestableBaseClient() { }

        public TestableBaseClient(int id, string language = "en-US")
            : base(id, language) { }

        public new Task<T?> Get<T>(
            string url,
            Dictionary<string, string?>? query = null,
            bool? priority = false,
            bool skipCache = false,
            TimeSpan? maxCacheAge = null
        )
            where T : class
        {
            return base.Get<T>(url, query, priority, skipCache, maxCacheAge);
        }

        public new Task<List<T>?> Paginated<T>(string url, int limit)
            where T : class
        {
            return base.Paginated<T>(url, limit);
        }
    }

    [Fact]
    public void Constructor_Default_CreatesClientWithZeroId()
    {
        // Arrange & Act
        using TestableBaseClient client = new();

        // Assert
        client.Id.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithIdAndLanguage_SetsPropertiesCorrectly()
    {
        // Arrange
        const int expectedId = 12345;
        const string language = "fr-FR";

        // Act
        using TestableBaseClient client = new(expectedId, language);

        // Assert
        client.Id.Should().Be(expectedId);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void Constructor_WithDifferentLanguages_CreatesClientSuccessfully(string language)
    {
        // Arrange & Act
        using TestableBaseClient client = new(ValidMovieId, language);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(ValidMovieId);
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
        using TestableBaseClient client = new(negativeId);

        // Assert
        client.Id.Should().Be(negativeId);
    }

    [Fact]
    public void Constructor_WithMaxIntId_SetsIdCorrectly()
    {
        // Arrange
        const int maxId = int.MaxValue;

        // Act
        using TestableBaseClient client = new(maxId);

        // Assert
        client.Id.Should().Be(maxId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid-language")]
    public void Constructor_WithInvalidLanguage_CreatesClientSuccessfully(string? language)
    {
        // Arrange & Act
        using TestableBaseClient client = new(ValidMovieId, language!);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(ValidMovieId);
    }

    [Fact]
    public void MultipleClients_CreatedSimultaneously_WorkIndependently()
    {
        // Arrange
        const int id1 = 100;
        const int id2 = 200;
        const string lang2 = "fr-FR";

        // Act
        using TestableBaseClient client1 = new(id1);
        using TestableBaseClient client2 = new(id2, lang2);

        // Assert
        client1.Id.Should().Be(id1);
        client2.Id.Should().Be(id2);
        client1.Should().NotBeSameAs(client2);
    }

    [Fact]
    public void Constructor_WithEmptyLanguage_CreatesClientSuccessfully()
    {
        // Arrange
        const string emptyLanguage = "";

        // Act
        using TestableBaseClient client = new(ValidMovieId, emptyLanguage);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(ValidMovieId);
    }

    [Fact]
    public void Client_AfterDispose_PropertiesStillAccessible()
    {
        // Arrange
        TestableBaseClient client = new(ValidMovieId);
        int originalId = client.Id;

        // Act
        client.Dispose();

        // Assert
        client.Id.Should().Be(originalId);
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
        using TestableBaseClient client = new(ValidMovieId, "en-US");
        Dictionary<string, string?> query = new() { ["language"] = "nl" };

        Task<TmdbGenreMovies?> task = client.Get<TmdbGenreMovies>(
            "genre/movie/list",
            query,
            priority: false
        );

        query["language"].Should().Be("nl");

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
