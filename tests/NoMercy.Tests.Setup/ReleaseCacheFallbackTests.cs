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

using System.Diagnostics;
using System.Net;
using Newtonsoft.Json;
using NoMercy.Setup.Dto;
using NoMercy.Setup.Server;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Setup;

/// <summary>
/// Tests for the last-known-good release-metadata cache in
/// <see cref="Binaries.GetLatestReleaseInfo"/>. This is what let ffmpeg provisioning
/// survive a GitHub API 403 for the real 0.1.414 user: a rate-limited fetch must
/// serve cached metadata instead of returning empty (which previously left ffmpeg
/// permanently un-installable), and must never block on GitHub's hourly reset window.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ReleaseCacheFallbackTests
{
    private static GithubReleaseResponse BuildRelease(string tagName = "v1.2.3") =>
        new()
        {
            TagName = tagName,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(days: -1),
            Assets =
            [
                new()
                {
                    Name = "asset.zip",
                    BrowserDownloadUrl = new(uriString: "https://example.com/asset.zip"),
                    Size = 42,
                },
            ],
        };

    private static HttpResponseMessage SuccessResponse(GithubReleaseResponse release) =>
        new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: JsonConvert.SerializeObject(value: release)),
        };

    // A reset far in the future — larger than the 2-minute wait cap — so the very
    // first rate-limited attempt exceeds the budget and falls back immediately
    // instead of sleeping. This is exactly the shape of GitHub's real hourly reset.
    private static HttpResponseMessage RateLimitedResponse()
    {
        HttpResponseMessage response = new(statusCode: HttpStatusCode.Forbidden)
        {
            Content = new StringContent(content: "rate limit exceeded"),
        };
        long resetUnix = DateTimeOffset.UtcNow.AddHours(hours: 1).ToUnixTimeSeconds();
        response.Headers.Add(name: "X-RateLimit-Reset", value: resetUnix.ToString());
        return response;
    }

    private static Binaries BuildBinaries(SequencedHttpHandler handler)
    {
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);
        HttpClient http = new(handler: handler);
        return new(driver: driver, storage: storage, httpClient: http);
    }

    [Fact]
    public async Task GetLatestReleaseInfo_RateLimited_NoCache_ReturnsEmpty()
    {
        string apiUrl =
            $"https://api.github.com/repos/nomercy-test/{Guid.NewGuid():N}/releases/latest";

        SequencedHttpHandler handler = new();
        handler.Enqueue(factory: RateLimitedResponse);
        Binaries binaries = BuildBinaries(handler: handler);

        GithubReleaseResponse result = await binaries.GetLatestReleaseInfo(apiUrl: apiUrl);

        result.Assets.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestReleaseInfo_SuccessThenRateLimited_ServesTheCachedResponse()
    {
        string apiUrl =
            $"https://api.github.com/repos/nomercy-test/{Guid.NewGuid():N}/releases/latest";
        GithubReleaseResponse release = BuildRelease(tagName: "v9.9.9");

        SequencedHttpHandler successHandler = new();
        successHandler.Enqueue(factory: () => SuccessResponse(release: release));
        Binaries successBinaries = BuildBinaries(handler: successHandler);

        GithubReleaseResponse firstResult = await successBinaries.GetLatestReleaseInfo(apiUrl: apiUrl);
        firstResult.Assets.Should().NotBeEmpty(because: "the live fetch succeeded and must be returned");

        // A second, independent Binaries instance (fresh HttpClient) rate-limited on
        // the same apiUrl must read the cache the first instance wrote to disk —
        // proving the cache survives across instances/boots, not just in-process state.
        SequencedHttpHandler rateLimitedHandler = new();
        rateLimitedHandler.Enqueue(factory: RateLimitedResponse);
        Binaries rateLimitedBinaries = BuildBinaries(handler: rateLimitedHandler);

        GithubReleaseResponse cachedResult = await rateLimitedBinaries.GetLatestReleaseInfo(apiUrl: apiUrl);

        cachedResult.Assets.Should().NotBeEmpty();
        cachedResult.TagName.Should().Be(expected: "v9.9.9");
    }

    [Fact]
    public async Task GetLatestReleaseInfo_RateLimited_WithCache_DoesNotHang()
    {
        string apiUrl =
            $"https://api.github.com/repos/nomercy-test/{Guid.NewGuid():N}/releases/latest";
        GithubReleaseResponse release = BuildRelease(tagName: "v4.5.6");

        SequencedHttpHandler seedHandler = new();
        seedHandler.Enqueue(factory: () => SuccessResponse(release: release));
        await BuildBinaries(handler: seedHandler).GetLatestReleaseInfo(apiUrl: apiUrl);

        SequencedHttpHandler rateLimitedHandler = new();
        rateLimitedHandler.Enqueue(factory: RateLimitedResponse);
        Binaries binaries = BuildBinaries(handler: rateLimitedHandler);

        Stopwatch stopwatch = Stopwatch.StartNew();
        GithubReleaseResponse result = await binaries.GetLatestReleaseInfo(apiUrl: apiUrl);
        stopwatch.Stop();

        result.Assets.Should().NotBeEmpty();
        // The reset in RateLimitedResponse is an hour out — a broken bound would
        // sleep for that entire window (or the full backoff schedule). A few
        // seconds proves the cache short-circuited the wait instead.
        stopwatch.Elapsed.Should().BeLessThan(expected: TimeSpan.FromSeconds(seconds: 10));
    }
}

/// <summary>
/// Serves pre-queued responses in order, one per request. Unlike a URL-keyed stub,
/// this lets a single test drive the SAME apiUrl through different outcomes across
/// successive <see cref="Binaries.GetLatestReleaseInfo"/> calls (success, then
/// rate-limited) without the handler needing to track call count itself.
/// </summary>
internal sealed class SequencedHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public void Enqueue(Func<HttpResponseMessage> factory) => _responses.Enqueue(item: factory);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (_responses.Count == 0)
            throw new InvalidOperationException(
                message: $"SequencedHttpHandler received an unexpected request to {request.RequestUri}"
            );

        return Task.FromResult(result: _responses.Dequeue()());
    }
}
