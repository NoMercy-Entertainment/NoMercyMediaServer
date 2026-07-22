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

using System.IO.Compression;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Subtitles;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.OpenSubtitles.Client;
using NoMercy.Providers.OpenSubtitles.Models;

namespace NoMercy.MediaProcessing.Subtitles;

/// <summary>
/// Concrete <see cref="IOpenSubtitlesProvider"/> that wraps the XML-RPC
/// <see cref="OpenSubtitlesClient"/>. Lives in MediaProcessing where both
/// the encoder contract and the provider XML-RPC client are accessible.
/// </summary>
public class OpenSubtitlesProvider : IOpenSubtitlesProvider
{
    private readonly ILogger<OpenSubtitlesProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private DateTimeOffset _rateLimitedUntil = DateTimeOffset.MinValue;
    private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromMinutes(minutes: 5);

    // Searches queue inside OpenSubtitlesBaseClient, keyed on its named client. Downloads hit a
    // different host and so never passed through it: a sweep issued them as fast as its loop ran.
    // One at a time, one second apart, is far above what a paced backlog needs and keeps the
    // interactive lane responsive — priority work drains before any of it.
    private static readonly Providers.Helpers.Queue DownloadQueue = new(
        options: new()
        {
            Concurrent = 1,
            Interval = 1000,
            Start = true,
        }
    );

    public bool IsRateLimited => DateTimeOffset.UtcNow < _rateLimitedUntil;

    public OpenSubtitlesProvider(
        ILogger<OpenSubtitlesProvider> logger,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByHashAsync(
        string movieHash,
        long fileSize,
        string[] languages,
        CancellationToken ct,
        bool priority = false
    )
    {
        if (IsRateLimited)
        {
            _logger.LogWarning(
                message: "OpenSubtitles rate-limited — skipping hash search until {Until}",
                args: _rateLimitedUntil
            );
            return [];
        }

        try
        {
            OpenSubtitlesClient client = new();
            await client.Login().ConfigureAwait(continueOnCapturedContext: false);

            List<OpenSubtitlesSearchResult> results = [];

            foreach (string language in ToBibliographicCodes(languages: languages))
            {
                ct.ThrowIfCancellationRequested();
                SubtitleSearchResponse? response = await client
                    .SearchSubtitlesByHash(movieHash: movieHash, fileSize: fileSize, language: language, priority: priority)
                    .ConfigureAwait(continueOnCapturedContext: false);

                results.AddRange(collection: OpenSubtitlesResponseParser.Parse(response: response, matchedBy: "moviehash"));
            }

            return results;
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
        {
            MarkRateLimited();
            throw new OpenSubtitlesRateLimitException();
        }
    }

    public async Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByFilenameAsync(
        string filename,
        string[] languages,
        CancellationToken ct,
        bool priority = false
    )
    {
        if (IsRateLimited)
        {
            _logger.LogWarning(
                message: "OpenSubtitles rate-limited — skipping filename search until {Until}",
                args: _rateLimitedUntil
            );
            return [];
        }

        try
        {
            OpenSubtitlesClient client = new();
            await client.Login().ConfigureAwait(continueOnCapturedContext: false);

            List<OpenSubtitlesSearchResult> results = [];

            foreach (string language in ToBibliographicCodes(languages: languages))
            {
                ct.ThrowIfCancellationRequested();
                SubtitleSearchResponse? response = await client
                    .SearchSubtitles(query: filename, language: language, priority: priority)
                    .ConfigureAwait(continueOnCapturedContext: false);

                results.AddRange(collection: OpenSubtitlesResponseParser.Parse(response: response, matchedBy: "filename"));
            }

            return results;
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
        {
            MarkRateLimited();
            throw new OpenSubtitlesRateLimitException();
        }
    }

    public async Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByTitleAsync(
        string title,
        int? season,
        int? episode,
        int? year,
        string[] languages,
        CancellationToken ct,
        bool priority = false
    )
    {
        if (IsRateLimited)
        {
            _logger.LogWarning(
                message: "OpenSubtitles rate-limited — skipping title search until {Until}",
                args: _rateLimitedUntil
            );
            return [];
        }

        try
        {
            OpenSubtitlesClient client = new();
            await client.Login().ConfigureAwait(continueOnCapturedContext: false);

            string query = BuildTitleQuery(title: title, season: season, episode: episode, year: year);
            List<OpenSubtitlesSearchResult> results = [];

            foreach (string language in ToBibliographicCodes(languages: languages))
            {
                ct.ThrowIfCancellationRequested();
                SubtitleSearchResponse? response = await client
                    .SearchSubtitles(query: query, language: language, priority: priority)
                    .ConfigureAwait(continueOnCapturedContext: false);

                results.AddRange(collection: OpenSubtitlesResponseParser.Parse(response: response, matchedBy: "title"));
            }

            return results;
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
        {
            MarkRateLimited();
            throw new OpenSubtitlesRateLimitException();
        }
    }

    public Task<byte[]> DownloadSubtitleAsync(
        string downloadUrl,
        CancellationToken ct,
        bool priority = false
    )
    {
        return DownloadQueue.Enqueue(task: () => FetchAsync(downloadUrl: downloadUrl, ct: ct), url: downloadUrl, priority: priority);
    }

    private async Task<byte[]> FetchAsync(string downloadUrl, CancellationToken ct)
    {
        HttpClient client = _httpClientFactory.CreateClient(name: HttpClientNames.OpenSubtitlesDownload);
        using HttpResponseMessage response = await client
            .GetAsync(requestUri: downloadUrl, cancellationToken: ct)
            .ConfigureAwait(continueOnCapturedContext: false);

        if ((int)response.StatusCode == 429)
        {
            MarkRateLimited();
            throw new OpenSubtitlesRateLimitException();
        }

        response.EnsureSuccessStatusCode();
        byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);

        return Decompress(payload: payload);
    }

    /// <summary>
    /// SubDownloadLink serves the cue file gzipped in the response body rather than as a
    /// Content-Encoding, so HttpClient never unwraps it. Sniffed by magic number instead of the
    /// URL suffix because the link is a VRF-signed redirect that carries no extension, and
    /// OpenSubtitles serves some candidates uncompressed.
    /// </summary>
    private static byte[] Decompress(byte[] payload)
    {
        if (payload.Length < 2 || payload[0] != 0x1F || payload[1] != 0x8B)
            return payload;

        using MemoryStream source = new(buffer: payload);
        using GZipStream gzip = new(stream: source, mode: CompressionMode.Decompress);
        using MemoryStream destination = new();
        gzip.CopyTo(destination: destination);
        return destination.ToArray();
    }

    private void MarkRateLimited()
    {
        _rateLimitedUntil = DateTimeOffset.UtcNow.Add(timeSpan: RateLimitBackoff);
        _logger.LogWarning(message: "OpenSubtitles rate-limited. Backoff until {Until}", args: _rateLimitedUntil);
    }

    /// <summary>
    /// sublanguageid only accepts ISO 639-2/B codes. Handed anything else — the 2-letter code the
    /// watch request carries, for one — OpenSubtitles drops the filter rather than erroring and
    /// answers with a fulltext match across every language, which reads downstream as "no results
    /// in the language asked for".
    /// </summary>
    private static IEnumerable<string> ToBibliographicCodes(string[] languages)
    {
        return languages
            .Select(selector: Culture.BibliographicLanguageCode)
            .Where(predicate: language => !string.IsNullOrWhiteSpace(value: language))
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildTitleQuery(string title, int? season, int? episode, int? year)
    {
        string query = title;
        if (season is not null && episode is not null)
            query += $" S{season:D2}E{episode:D2}";
        else if (season is not null)
            query += $" Season {season}";
        if (year is not null)
            query += $" {year}";
        return query;
    }
}
