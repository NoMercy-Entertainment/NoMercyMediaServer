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

using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Client;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Pins <see cref="TmdbBaseClient.Paginated{T}"/>'s page-fetch bound.
/// <c>Parallel.ForAsync</c> takes an exclusive upper bound, so the raw page
/// count must be adjusted before being passed in or the last page is silently
/// dropped from every popular/discover listing.
/// </summary>
[Collection("HttpClientProvider")]
public class TmdbBaseClientPaginationTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        HttpClientProvider.Reset();
        _serviceProvider?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FakeItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }
    }

    private sealed class TestableBaseClient : TmdbBaseClient
    {
        public new Task<List<T>?> Paginated<T>(string url, int limit)
            where T : class
        {
            return base.Paginated<T>(url, limit);
        }
    }

    /// <summary>
    /// <see cref="HttpClientProvider"/> is a process-wide static, so this
    /// registration is visible to every other test running concurrently in
    /// this assembly. Only intercept our own synthetic "test/paginated" path —
    /// anything else (a real TMDB path from an unrelated concurrently-running
    /// test) is forwarded to a real handler so it never sees fabricated data.
    /// </summary>
    private sealed class TotalPagesHandler(int totalPages) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _passthrough = new(new HttpClientHandler());

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (
                request.RequestUri is null
                || !request.RequestUri.AbsolutePath.Contains("test/paginated")
            )
                return _passthrough.SendAsync(request, cancellationToken);

            int page = ExtractPage(request.RequestUri);
            string json = $$"""
                {"page": {{page}}, "total_pages": {{totalPages}}, "total_results": {{totalPages}}, "results": [{"id": {{page}}}]}
                """;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                }
            );
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _passthrough.Dispose();

            base.Dispose(disposing);
        }

        private static int ExtractPage(Uri? uri)
        {
            if (uri is null)
                return 1;

            foreach (
                string pair in uri
                    .Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
            )
            {
                string[] parts = pair.Split('=', 2);
                if (parts[0] == "page" && parts.Length == 2 && int.TryParse(parts[1], out int page))
                    return page;
            }

            return 1;
        }
    }

    private TestableBaseClient CreateClient(int totalPages)
    {
        TestApiKeyStore.Instance.TmdbToken = "test-token";

        ServiceCollection services = new();
        services
            .AddHttpClient(
                HttpClientNames.Tmdb,
                client => client.BaseAddress = new("https://api.themoviedb.org/3/")
            )
            .ConfigurePrimaryHttpMessageHandler(() => new TotalPagesHandler(totalPages));

        _serviceProvider = services.BuildServiceProvider();
        HttpClientProvider.Initialize(_serviceProvider.GetRequiredService<IHttpClientFactory>());

        return new();
    }

    // Responses are cached by URL through CacheController, and that cache is
    // shared across every test in this process. Each test must use a unique
    // synthetic path or the first test's total_pages poisons the next test's
    // first-page read. The handler matches on the "test/paginated" prefix.
    private static string UniquePaginatedUrl() => $"test/paginated/{Guid.NewGuid():N}";

    [Fact]
    public async Task Paginated_WhenLimitExceedsTotalPages_FetchesEveryPage()
    {
        using TestableBaseClient client = CreateClient(totalPages: 3);

        List<FakeItem>? results = await client.Paginated<FakeItem>(UniquePaginatedUrl(), 10);

        results.Should().NotBeNull();
        results!.Select(item => item.Id).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task Paginated_LimitBelowTotalPages_FetchesUpToLimit()
    {
        using TestableBaseClient client = CreateClient(totalPages: 500);

        List<FakeItem>? results = await client.Paginated<FakeItem>(UniquePaginatedUrl(), 4);

        results.Should().NotBeNull();
        results!.Select(item => item.Id).Should().BeEquivalentTo([1, 2, 3, 4]);
    }
}
