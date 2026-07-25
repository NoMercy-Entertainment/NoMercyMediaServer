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

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Wraps <see cref="IOpenSubtitlesProvider"/> and translates provider DTOs into
/// <see cref="SubtitleCandidate"/> records. Encoder code never sees provider types.
/// </summary>
public class OpenSubtitlesAdapter(
    IOpenSubtitlesProvider provider,
    ILogger<OpenSubtitlesAdapter>? logger = null
) : IOpenSubtitlesAdapter
{
    private const string ProviderName = "OpenSubtitles";

    public bool IsRateLimited => provider.IsRateLimited;

    public async Task<IReadOnlyList<SubtitleCandidate>> SearchByHashAsync(
        string movieHash,
        long fileSize,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false,
        bool priority = false
    )
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            IReadOnlyList<OpenSubtitlesSearchResult> results = await provider
                .SearchByHashAsync(movieHash, fileSize, languages, cts.Token, priority)
                .ConfigureAwait(false);

            return Translate(results, trustedOnly);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            logger?.LogWarning("OpenSubtitles rate-limited during hash search");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("OpenSubtitles hash search timed out");
            return [];
        }
    }

    public async Task<IReadOnlyList<SubtitleCandidate>> SearchByFilenameAsync(
        string filename,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false,
        bool priority = false
    )
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            IReadOnlyList<OpenSubtitlesSearchResult> results = await provider
                .SearchByFilenameAsync(filename, languages, cts.Token, priority)
                .ConfigureAwait(false);

            return Translate(results, trustedOnly);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            logger?.LogWarning("OpenSubtitles rate-limited during filename search");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("OpenSubtitles filename search timed out");
            return [];
        }
    }

    public async Task<IReadOnlyList<SubtitleCandidate>> SearchByTitleAsync(
        string title,
        int? season,
        int? episode,
        int? year,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false,
        bool priority = false
    )
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            IReadOnlyList<OpenSubtitlesSearchResult> results = await provider
                .SearchByTitleAsync(title, season, episode, year, languages, cts.Token, priority)
                .ConfigureAwait(false);

            return Translate(results, trustedOnly);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            logger?.LogWarning("OpenSubtitles rate-limited during title search");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("OpenSubtitles title search timed out");
            return [];
        }
    }

    public async Task<byte[]> DownloadAsync(
        SubtitleCandidate candidate,
        CancellationToken ct,
        bool priority = false
    )
    {
        return await provider
            .DownloadSubtitleAsync(candidate.DownloadUrl, ct, priority)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<SubtitleCandidate> Translate(
        IReadOnlyList<OpenSubtitlesSearchResult> results,
        bool trustedOnly
    )
    {
        List<SubtitleCandidate> candidates = [];

        foreach (OpenSubtitlesSearchResult r in results)
        {
            bool isTrusted = r.SubFromTrusted == "1";
            if (trustedOnly && !isTrusted)
                continue;

            double rating = TryParseDouble(r.SubRating) ?? 0.0;
            int downloads = TryParseInt(r.SubDownloadsCnt) ?? 0;
            double? fps = TryParseDouble(r.MovieFPS);

            candidates.Add(
                new(
                    ProviderName,
                    r.Language,
                    rating,
                    downloads,
                    isTrusted,
                    fps,
                    r.SubDownloadLink ?? string.Empty,
                    NormalizeFormat(r.SubFormat),
                    r.SubFileName,
                    r.MovieReleaseName,
                    r.SubHearingImpaired == "1",
                    r.UserNickName
                )
            );
        }

        return candidates;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return double.TryParse(
            value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double d
        )
            ? d
            : null;
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value, out int i) ? i : null;
    }

    private static string NormalizeFormat(string? format)
    {
        return (format ?? "srt").ToLowerInvariant() switch
        {
            "subrip" => "srt",
            "webvtt" => "vtt",
            string f => f,
        };
    }
}
