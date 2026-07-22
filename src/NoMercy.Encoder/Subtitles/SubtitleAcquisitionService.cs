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

        string[] languages = ResolveLanguages(request: request);
        if (languages.Length == 0)
            return [];

        List<AcquiredSubtitle> acquired = [];

        try
        {
            (IReadOnlyList<SubtitleCandidate> candidates, bool wasHashStrategy) =
                await RunStrategyChainAsync(request: request, languages: languages, ct: ct).ConfigureAwait(continueOnCapturedContext: false);

            IReadOnlyList<SubtitleCandidate> filtered = ApplyFilters(
                candidates: candidates,
                config: request.Config,
                sourceFps: request.SourceFps
            );
            IReadOnlyList<SubtitleCandidate> selected = SelectTopPerLanguage(
                candidates: filtered,
                maxPerLanguage: request.Config.MaxPerLanguage
            );

            foreach (SubtitleCandidate candidate in selected)
            {
                AcquiredSubtitle? result = await DownloadCandidateAsync(
                        candidate: candidate,
                        request: request,
                        wasHashStrategy: wasHashStrategy,
                        ct: ct
                    )
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (result is not null)
                    acquired.Add(item: result);
            }
        }
        catch (OpenSubtitlesRateLimitException ex)
        {
            logger.LogWarning(exception: ex, message: "OpenSubtitles rate-limited — subtitle acquisition skipped");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(message: "Subtitle acquisition cancelled");
            return [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(exception: ex, message: "Subtitle acquisition failed — encoding continues without subs");
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
            .Where(predicate: lang =>
                !request.LanguagesAlreadyInSource.Contains(value: lang, comparer: StringComparer.OrdinalIgnoreCase)
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
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            cts.CancelAfter(delay: timeout);

            string? hash = ComputeHash(sourcePath: request.SourcePath, fileSize: request.SourceFileSize);
            if (hash is null)
                logger.LogWarning(
                    message: "Could not read {SourcePath} to compute a moviehash — skipping the hash "
                             + "strategy for this source",
                    args: request.SourcePath
                );

            if (hash is not null)
            {
                IReadOnlyList<SubtitleCandidate> hashResults = await adapter
                    .SearchByHashAsync(
                        movieHash: hash,
                        fileSize: request.SourceFileSize,
                        languages: languages,
                        timeout: timeout,
                        ct: cts.Token,
                        trustedOnly: trustedOnly
                    )
                    .ConfigureAwait(continueOnCapturedContext: false);

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
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            cts.CancelAfter(delay: timeout);

            IReadOnlyList<SubtitleCandidate> filenameResults = await adapter
                .SearchByFilenameAsync(
                    filename: request.SourceFilename,
                    languages: languages,
                    timeout: timeout,
                    ct: cts.Token,
                    trustedOnly: trustedOnly
                )
                .ConfigureAwait(continueOnCapturedContext: false);

            if (filenameResults.Count > 0)
                return (filenameResults, WasHashStrategy: false);
        }

        if (
            strategy
            is SubtitleMatchStrategy.HashThenFilenameThenTitle
                or SubtitleMatchStrategy.TitleOnly
        )
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            cts.CancelAfter(delay: timeout);

            IReadOnlyList<SubtitleCandidate> titleResults = await adapter
                .SearchByTitleAsync(
                    title: request.MediaTitle,
                    season: request.Season,
                    episode: request.Episode,
                    year: request.Year,
                    languages: languages,
                    timeout: timeout,
                    ct: cts.Token,
                    trustedOnly: trustedOnly
                )
                .ConfigureAwait(continueOnCapturedContext: false);

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
            .Where(predicate: c => c.Rating >= config.MinRating)
            .Where(predicate: c => c.Downloads >= config.MinDownloads)
            .Where(predicate: c => !config.TrustedUploadersOnly || c.IsTrustedUploader)
            .Where(predicate: c =>
                !config.RequireMatchingFps
                || sourceFps is null
                || c.Fps is null
                || Math.Abs(value: c.Fps.Value - sourceFps.Value) <= FpsTolerance
            )
            .ToList();
    }

    private static IReadOnlyList<SubtitleCandidate> SelectTopPerLanguage(
        IReadOnlyList<SubtitleCandidate> candidates,
        int maxPerLanguage
    )
    {
        return candidates
            .GroupBy(keySelector: c => c.Language, comparer: StringComparer.OrdinalIgnoreCase)
            .SelectMany(selector: g =>
                g.OrderByDescending(keySelector: c => c.Rating * Math.Log10(d: c.Downloads + 1))
                    .Take(count: maxPerLanguage)
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
            byte[] bytes = await adapter.DownloadAsync(candidate: candidate, ct: ct).ConfigureAwait(continueOnCapturedContext: false);

            string tempDir = "subtitles/temp";
            string fileName = $"{Guid.NewGuid():N}_{candidate.Language}.{candidate.Format}";
            string relativePath = storage.CombinePath(parent: tempDir, child: fileName);

            storage.CreateDirectory(path: tempDir);
            await storage.WriteAsync(path: relativePath, bytes: bytes, ct: ct).ConfigureAwait(continueOnCapturedContext: false);

            string localPath = storage.GetFullPath(path: relativePath);
            bool isExactMatch = ComputeExactMatch(candidate: candidate, request: request, wasHashStrategy: wasHashStrategy);

            return new(
                Language: candidate.Language,
                LocalPath: localPath,
                Provider: candidate.Provider,
                IsExactMatch: isExactMatch,
                Rating: candidate.Rating,
                Downloads: candidate.Downloads,
                Format: candidate.Format
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Failed to download subtitle for language {Language} — skipping",
                args: candidate.Language
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
            if (Math.Abs(value: candidate.Fps.Value - request.SourceFps.Value) > FpsTolerance)
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
            using FileStream fs = File.OpenRead(path: sourcePath);
            ulong hash = MovieHashHelper.ComputeMovieHash(stream: fs, fileSize: fileSize);
            return MovieHashHelper.FormatHash(hash: hash);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
