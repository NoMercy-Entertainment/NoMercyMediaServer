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
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            cts.CancelAfter(delay: timeout);

            IReadOnlyList<OpenSubtitlesSearchResult> results = await provider
                .SearchByHashAsync(movieHash: movieHash, fileSize: fileSize, languages: languages, ct: cts.Token, priority: priority)
                .ConfigureAwait(continueOnCapturedContext: false);

            return Translate(results: results, trustedOnly: trustedOnly);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            logger?.LogWarning(message: "OpenSubtitles rate-limited during hash search");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation(message: "OpenSubtitles hash search timed out");
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
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            cts.CancelAfter(delay: timeout);

            IReadOnlyList<OpenSubtitlesSearchResult> results = await provider
                .SearchByFilenameAsync(filename: filename, languages: languages, ct: cts.Token, priority: priority)
                .ConfigureAwait(continueOnCapturedContext: false);

            return Translate(results: results, trustedOnly: trustedOnly);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            logger?.LogWarning(message: "OpenSubtitles rate-limited during filename search");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation(message: "OpenSubtitles filename search timed out");
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
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            cts.CancelAfter(delay: timeout);

            IReadOnlyList<OpenSubtitlesSearchResult> results = await provider
                .SearchByTitleAsync(title: title, season: season, episode: episode, year: year, languages: languages, ct: cts.Token, priority: priority)
                .ConfigureAwait(continueOnCapturedContext: false);

            return Translate(results: results, trustedOnly: trustedOnly);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            logger?.LogWarning(message: "OpenSubtitles rate-limited during title search");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation(message: "OpenSubtitles title search timed out");
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
            .DownloadSubtitleAsync(downloadUrl: candidate.DownloadUrl, ct: ct, priority: priority)
            .ConfigureAwait(continueOnCapturedContext: false);
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

            double rating = TryParseDouble(value: r.SubRating) ?? 0.0;
            int downloads = TryParseInt(value: r.SubDownloadsCnt) ?? 0;
            double? fps = TryParseDouble(value: r.MovieFPS);

            candidates.Add(
                item: new(
                    Provider: ProviderName,
                    Language: r.Language,
                    Rating: rating,
                    Downloads: downloads,
                    IsTrustedUploader: isTrusted,
                    Fps: fps,
                    DownloadUrl: r.SubDownloadLink ?? string.Empty,
                    Format: NormalizeFormat(format: r.SubFormat),
                    FileName: r.SubFileName,
                    ReleaseName: r.MovieReleaseName,
                    HearingImpaired: r.SubHearingImpaired == "1",
                    Uploader: r.UserNickName
                )
            );
        }

        return candidates;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value: value))
            return null;
        return double.TryParse(
            s: value,
            style: System.Globalization.NumberStyles.Any,
            provider: System.Globalization.CultureInfo.InvariantCulture,
            result: out double d
        )
            ? d
            : null;
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value: value))
            return null;
        return int.TryParse(s: value, result: out int i) ? i : null;
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
