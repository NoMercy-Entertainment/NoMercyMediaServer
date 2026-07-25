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
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Orchestrates subtitle acquisition: strategy chain, scoring, selection, download.
/// </summary>
public class SubtitleAcquisitionService(
    IOpenSubtitlesAdapter adapter,
    IStorage storage,
    ILogger<SubtitleAcquisitionService> logger
) : ISubtitleAcquisitionService
{
    private const double FpsTolerance = 0.1;

    public async Task<IReadOnlyList<AcquiredSubtitle>> AcquireAsync(
        AcquisitionRequest request,
        CancellationToken ct
    )
    {
        if (!request.Config.Enabled)
            return [];

        string[] languages = ResolveLanguages(request);
        if (languages.Length == 0)
            return [];

        List<AcquiredSubtitle> acquired = [];

        try
        {
            (IReadOnlyList<SubtitleCandidate> candidates, bool wasHashStrategy) =
                await RunStrategyChainAsync(request, languages, ct).ConfigureAwait(false);

            IReadOnlyList<SubtitleCandidate> filtered = ApplyFilters(
                candidates,
                request.Config,
                request.SourceFps
            );
            IReadOnlyList<SubtitleCandidate> selected = SelectTopPerLanguage(
                filtered,
                request.Config.MaxPerLanguage
            );

            foreach (SubtitleCandidate candidate in selected)
            {
                AcquiredSubtitle? result = await DownloadCandidateAsync(
                        candidate,
                        request,
                        wasHashStrategy,
                        ct
                    )
                    .ConfigureAwait(false);

                if (result is not null)
                    acquired.Add(result);
            }
        }
        catch (OpenSubtitlesRateLimitException ex)
        {
            logger.LogWarning(ex, "OpenSubtitles rate-limited — subtitle acquisition skipped");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Subtitle acquisition cancelled");
            return [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Subtitle acquisition failed — encoding continues without subs");
            return [];
        }

        return acquired;
    }

    private static string[] ResolveLanguages(AcquisitionRequest request)
    {
        string[] languages = request.Config.Languages.Length > 0 ? request.Config.Languages : [];

        if (!request.Config.FillMissingOnly)
            return languages;

        return languages
            .Where(lang =>
                !request.LanguagesAlreadyInSource.Contains(lang, StringComparer.OrdinalIgnoreCase)
            )
            .ToArray();
    }

    private async Task<(
        IReadOnlyList<SubtitleCandidate> Candidates,
        bool WasHashStrategy
    )> RunStrategyChainAsync(AcquisitionRequest request, string[] languages, CancellationToken ct)
    {
        SubtitleMatchStrategy strategy = request.Config.Strategy;
        TimeSpan timeout = request.Config.PerRequestTimeout;
        bool trustedOnly = request.Config.TrustedUploadersOnly;

        if (
            strategy
            is SubtitleMatchStrategy.HashOnly
                or SubtitleMatchStrategy.HashThenFilename
                or SubtitleMatchStrategy.HashThenFilenameThenTitle
        )
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            string? hash = ComputeHash(request.SourcePath, request.SourceFileSize);
            if (hash is null)
                logger.LogWarning(
                    "Could not read {SourcePath} to compute a moviehash — skipping the hash "
                             + "strategy for this source",
                    request.SourcePath
                );

            if (hash is not null)
            {
                IReadOnlyList<SubtitleCandidate> hashResults = await adapter
                    .SearchByHashAsync(
                        hash,
                        request.SourceFileSize,
                        languages,
                        timeout,
                        cts.Token,
                        trustedOnly
                    )
                    .ConfigureAwait(false);

                if (hashResults.Count > 0)
                    return (hashResults, WasHashStrategy: true);
            }
        }

        if (
            strategy
            is SubtitleMatchStrategy.HashThenFilename
                or SubtitleMatchStrategy.HashThenFilenameThenTitle
        )
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            IReadOnlyList<SubtitleCandidate> filenameResults = await adapter
                .SearchByFilenameAsync(
                    request.SourceFilename,
                    languages,
                    timeout,
                    cts.Token,
                    trustedOnly
                )
                .ConfigureAwait(false);

            if (filenameResults.Count > 0)
                return (filenameResults, WasHashStrategy: false);
        }

        if (
            strategy
            is SubtitleMatchStrategy.HashThenFilenameThenTitle
                or SubtitleMatchStrategy.TitleOnly
        )
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            IReadOnlyList<SubtitleCandidate> titleResults = await adapter
                .SearchByTitleAsync(
                    request.MediaTitle,
                    request.Season,
                    request.Episode,
                    request.Year,
                    languages,
                    timeout,
                    cts.Token,
                    trustedOnly
                )
                .ConfigureAwait(false);

            return (titleResults, WasHashStrategy: false);
        }

        return ([], WasHashStrategy: false);
    }

    private static IReadOnlyList<SubtitleCandidate> ApplyFilters(
        IReadOnlyList<SubtitleCandidate> candidates,
        SubtitleAcquisitionConfig config,
        double? sourceFps
    )
    {
        return candidates
            .Where(c => c.Rating >= config.MinRating)
            .Where(c => c.Downloads >= config.MinDownloads)
            .Where(c => !config.TrustedUploadersOnly || c.IsTrustedUploader)
            .Where(c =>
                !config.RequireMatchingFps
                || sourceFps is null
                || c.Fps is null
                || Math.Abs(c.Fps.Value - sourceFps.Value) <= FpsTolerance
            )
            .ToList();
    }

    private static IReadOnlyList<SubtitleCandidate> SelectTopPerLanguage(
        IReadOnlyList<SubtitleCandidate> candidates,
        int maxPerLanguage
    )
    {
        return candidates
            .GroupBy(c => c.Language, StringComparer.OrdinalIgnoreCase)
            .SelectMany(g =>
                g.OrderByDescending(c => c.Rating * Math.Log10(c.Downloads + 1))
                    .Take(maxPerLanguage)
            )
            .ToList();
    }

    private async Task<AcquiredSubtitle?> DownloadCandidateAsync(
        SubtitleCandidate candidate,
        AcquisitionRequest request,
        bool wasHashStrategy,
        CancellationToken ct
    )
    {
        try
        {
            byte[] bytes = await adapter.DownloadAsync(candidate, ct).ConfigureAwait(false);

            string tempDir = "subtitles/temp";
            string fileName = $"{Guid.NewGuid():N}_{candidate.Language}.{candidate.Format}";
            string relativePath = storage.CombinePath(tempDir, fileName);

            storage.CreateDirectory(tempDir);
            await storage.WriteAsync(relativePath, bytes, ct).ConfigureAwait(false);

            string localPath = storage.GetFullPath(relativePath);
            bool isExactMatch = ComputeExactMatch(candidate, request, wasHashStrategy);

            return new(
                candidate.Language,
                localPath,
                candidate.Provider,
                isExactMatch,
                candidate.Rating,
                candidate.Downloads,
                candidate.Format
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to download subtitle for language {Language} — skipping",
                candidate.Language
            );
            return null;
        }
    }

    private static bool ComputeExactMatch(
        SubtitleCandidate candidate,
        AcquisitionRequest request,
        bool wasHashStrategy
    )
    {
        if (!wasHashStrategy)
            return false;

        if (request.SourceFps is not null && candidate.Fps is not null)
        {
            if (Math.Abs(candidate.Fps.Value - request.SourceFps.Value) > FpsTolerance)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns null when the source cannot be read, so the caller skips the hash strategy instead
    /// of querying with a value that cannot match. Formatting the file size as a hash looks like a
    /// result but is not one: every such search is a guaranteed miss that still costs a request,
    /// and a chance match would be reported as an exact one.
    /// </summary>
    private static string? ComputeHash(string sourcePath, long fileSize)
    {
        try
        {
            using FileStream fs = File.OpenRead(sourcePath);
            ulong hash = MovieHashHelper.ComputeMovieHash(fs, fileSize);
            return MovieHashHelper.FormatHash(hash);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
