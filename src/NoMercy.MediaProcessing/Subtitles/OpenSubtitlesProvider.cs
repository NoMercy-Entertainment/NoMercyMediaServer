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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Subtitles;
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
    private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromMinutes(5);

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
        CancellationToken ct
    )
    {
        if (IsRateLimited)
        {
            _logger.LogWarning(
                "OpenSubtitles rate-limited — skipping hash search until {Until}",
                _rateLimitedUntil
            );
            return [];
        }

        try
        {
            OpenSubtitlesClient client = new();
            await client.Login().ConfigureAwait(false);

            List<OpenSubtitlesSearchResult> results = [];

            foreach (string language in languages)
            {
                ct.ThrowIfCancellationRequested();
                SubtitleSearchResponse? response = await client
                    .SearchSubtitlesByHash(movieHash, fileSize, language)
                    .ConfigureAwait(false);

                results.AddRange(ParseResponse(response, "moviehash"));
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
        CancellationToken ct
    )
    {
        if (IsRateLimited)
        {
            _logger.LogWarning(
                "OpenSubtitles rate-limited — skipping filename search until {Until}",
                _rateLimitedUntil
            );
            return [];
        }

        try
        {
            OpenSubtitlesClient client = new();
            await client.Login().ConfigureAwait(false);

            List<OpenSubtitlesSearchResult> results = [];

            foreach (string language in languages)
            {
                ct.ThrowIfCancellationRequested();
                SubtitleSearchResponse? response = await client
                    .SearchSubtitles(filename, language)
                    .ConfigureAwait(false);

                results.AddRange(ParseResponse(response, "filename"));
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
        CancellationToken ct
    )
    {
        if (IsRateLimited)
        {
            _logger.LogWarning(
                "OpenSubtitles rate-limited — skipping title search until {Until}",
                _rateLimitedUntil
            );
            return [];
        }

        try
        {
            OpenSubtitlesClient client = new();
            await client.Login().ConfigureAwait(false);

            string query = BuildTitleQuery(title, season, episode, year);
            List<OpenSubtitlesSearchResult> results = [];

            foreach (string language in languages)
            {
                ct.ThrowIfCancellationRequested();
                SubtitleSearchResponse? response = await client
                    .SearchSubtitles(query, language)
                    .ConfigureAwait(false);

                results.AddRange(ParseResponse(response, "title"));
            }

            return results;
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
        {
            MarkRateLimited();
            throw new OpenSubtitlesRateLimitException();
        }
    }

    public async Task<byte[]> DownloadSubtitleAsync(string downloadUrl, CancellationToken ct)
    {
        HttpClient client = _httpClientFactory.CreateClient(
            Providers.Helpers.HttpClientNames.OpenSubtitlesDownload
        );
        using HttpResponseMessage response = await client
            .GetAsync(downloadUrl, ct)
            .ConfigureAwait(false);

        if ((int)response.StatusCode == 429)
        {
            MarkRateLimited();
            throw new OpenSubtitlesRateLimitException();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    private void MarkRateLimited()
    {
        _rateLimitedUntil = DateTimeOffset.UtcNow.Add(RateLimitBackoff);
        _logger.LogWarning("OpenSubtitles rate-limited. Backoff until {Until}", _rateLimitedUntil);
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

    /// <summary>
    /// Parses the XML-RPC SubtitleSearchResponse into normalized results.
    /// </summary>
    private static IEnumerable<OpenSubtitlesSearchResult> ParseResponse(
        SubtitleSearchResponse? response,
        string matchedBy
    )
    {
        if (response?.Params is null)
            yield break;

        foreach (SubtitleSearchResponseParam param in response.Params)
        {
            if (param.Value.ArrayData.Values is null)
                continue;

            foreach (SubtitleSearchResponseMemberValue item in param.Value.ArrayData.Values)
            {
                if (item.InnerStruct.Members is null)
                    continue;

                Dictionary<string, string> members = item
                    .InnerStruct.Members.Where(m => m.Name is not null)
                    .ToDictionary(
                        m => m.Name!,
                        m => m.MemberValue.StringValue ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase
                    );

                string language =
                    members.GetValueOrDefault("SubLanguageID")
                    ?? members.GetValueOrDefault("ISO639")
                    ?? "und";

                yield return new(
                    Language: language,
                    SubRating: members.GetValueOrDefault("SubRating"),
                    SubDownloadsCnt: members.GetValueOrDefault("SubDownloadsCnt"),
                    SubFromTrusted: members.GetValueOrDefault("SubFromTrusted"),
                    MovieFPS: members.GetValueOrDefault("MovieFPS"),
                    SubDownloadLink: members.GetValueOrDefault("SubDownloadLink"),
                    SubFormat: members.GetValueOrDefault("SubFormat"),
                    MatchedBy: members.GetValueOrDefault("MatchedBy") ?? matchedBy,
                    SubFileName: members.GetValueOrDefault("SubFileName"),
                    MovieReleaseName: members.GetValueOrDefault("MovieReleaseName"),
                    SubHearingImpaired: members.GetValueOrDefault("SubHearingImpaired"),
                    UserNickName: members.GetValueOrDefault("UserNickName")
                );
            }
        }
    }
}
