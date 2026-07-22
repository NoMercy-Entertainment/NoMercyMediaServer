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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Storage;

namespace NoMercy.Encoder.Subscribers;

/// <summary>
/// Listens for <see cref="LibraryScanCompletedEvent"/> and runs chromaprint
/// fingerprinting + intro/outro detection for all episodes in each season
/// that belongs to the scanned library.
///
/// Skips episodes that already have a <c>Source="manual"</c> row of the same
/// type — manual annotations always win.
///
/// Opt-out: set <see cref="EncoderOptions.EnableIntroDetectSubscriber"/> = false.
/// </summary>
public class IntroDetectSubscriber : IDisposable
{
    private readonly IAudioFingerprinter _fingerprinter;
    private readonly IIntroDetector _introDetector;
    private readonly EncoderOptions _options;
    private readonly ILogger<IntroDetectSubscriber> _logger;
    private readonly IStorage _storage;
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly List<IDisposable> _subscriptions = [];

    // Fingerprint the first 3 minutes for intro detection.
    private static readonly FingerprintWindow IntroWindow = new(
        Start: TimeSpan.Zero,
        Duration: TimeSpan.FromMinutes(minutes: 3)
    );

    // Fingerprint the last 4 minutes for outro detection.
    private static readonly FingerprintWindow OutroWindow = new(
        Start: TimeSpan.FromMinutes(minutes: -4),
        Duration: TimeSpan.FromMinutes(minutes: 4)
    );

    public IntroDetectSubscriber(
        IEventBus eventBus,
        IAudioFingerprinter fingerprinter,
        IIntroDetector introDetector,
        EncoderOptions options,
        ILogger<IntroDetectSubscriber> logger,
        IStorage storage,
        IDbContextFactory<MediaContext> contextFactory
    )
    {
        _fingerprinter = fingerprinter;
        _introDetector = introDetector;
        _options = options;
        _logger = logger;
        _storage = storage;
        _contextFactory = contextFactory;

        _subscriptions.Add(item: eventBus.Subscribe<LibraryScanCompletedEvent>(handler: OnLibraryScanCompleted));
    }

    internal async Task OnLibraryScanCompleted(
        LibraryScanCompletedEvent @event,
        CancellationToken ct
    )
    {
        if (!_options.EnableIntroDetectSubscriber)
            return;

        // Load all TV seasons whose show belongs to this library.
        List<int> seasonIds;
        await using (MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct))
        {
            seasonIds = await context
                .Seasons.AsNoTracking()
                .Where(predicate: s =>
                    context.LibraryTv.Any(lt =>
                        lt.LibraryId == @event.LibraryId && lt.TvId == s.TvId
                    )
                )
                .Select(selector: s => s.Id)
                .ToListAsync(cancellationToken: ct);
        }

        // Intro/outro detection is a TV-episode feature. A library with no TV
        // seasons — music, movies, or an empty library — has nothing to detect, so
        // skip it quietly instead of announcing a run that immediately does nothing.
        if (seasonIds.Count == 0)
        {
            _logger.LogDebug(
                message: "IntroDetect: no TV content in library {LibraryName} ({LibraryId}) — skipping", args: [@event.LibraryName, @event.LibraryId]
            );
            return;
        }

        _logger.LogInformation(
            message: "IntroDetect: starting for library {LibraryName} ({LibraryId}) — {SeasonCount} season(s)", args: [@event.LibraryName, @event.LibraryId, seasonIds.Count]
        );

        foreach (int seasonId in seasonIds)
        {
            if (ct.IsCancellationRequested)
                break;

            await ProcessSeasonAsync(seasonId: seasonId, ct: ct);
        }
    }

    private async Task ProcessSeasonAsync(int seasonId, CancellationToken ct)
    {
        List<Episode> episodes;
        HashSet<int> episodesWithIntro;
        HashSet<int> episodesWithOutro;

        await using (MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct))
        {
            episodes = await context
                .Episodes.AsNoTracking()
                .Where(predicate: e => e.SeasonId == seasonId)
                .OrderBy(keySelector: e => e.EpisodeNumber)
                .ToListAsync(cancellationToken: ct);

            // Two-step to avoid SQLite APPLY: fetch episode IDs with existing segments first.
            List<int> episodeIds = episodes.Select(selector: e => e.Id).ToList();

            // Every existing segment regardless of Source — a prior detector
            // run's rows must block re-insertion on a rescan just as much as a
            // manual one, or a repeated LibraryScanCompletedEvent duplicates
            // every segment row on each pass.
            List<ContentSegment> existingSegments = await context
                .ContentSegments.AsNoTracking()
                .Where(predicate: cs => cs.EpisodeId != null && episodeIds.Contains(cs.EpisodeId!.Value))
                .ToListAsync(cancellationToken: ct);

            episodesWithIntro = existingSegments
                .Where(predicate: cs => cs.SegmentType == ContentSegmentType.Intro)
                .Select(selector: cs => cs.EpisodeId!.Value)
                .ToHashSet();

            episodesWithOutro = existingSegments
                .Where(predicate: cs => cs.SegmentType == ContentSegmentType.Outro)
                .Select(selector: cs => cs.EpisodeId!.Value)
                .ToHashSet();
        }

        if (episodes.Count < 2)
        {
            _logger.LogDebug(
                message: "IntroDetect: season {SeasonId} has fewer than 2 episodes — skipping",
                args: seasonId
            );
            return;
        }

        // Resolve source file paths for each episode.
        List<(Episode Episode, string FilePath)> episodeFiles = await ResolveFilePathsAsync(
            episodes: episodes,
            ct: ct
        );

        if (episodeFiles.Count < 2)
            return;

        // Fingerprint intro windows.
        bool needIntro = episodeFiles.Any(predicate: ef => !episodesWithIntro.Contains(item: ef.Episode.Id));
        bool needOutro = episodeFiles.Any(predicate: ef => !episodesWithOutro.Contains(item: ef.Episode.Id));

        if (!needIntro && !needOutro)
        {
            _logger.LogDebug(
                message: "IntroDetect: all episodes in season {SeasonId} already have manual segments",
                args: seasonId
            );
            return;
        }

        List<ContentSegment> newSegments = [];

        if (needIntro)
        {
            List<(Episode Episode, AudioFingerprint Fp)> introFingerprints = await FingerprintAsync(
                episodeFiles: episodeFiles,
                window: IntroWindow,
                ct: ct
            );

            if (introFingerprints.Count >= 2)
            {
                IntroMarker? marker = _introDetector.DetectIntro(
                    episodeFingerprints: introFingerprints.Select(selector: x => x.Fp).ToList()
                );

                if (marker is not null)
                {
                    foreach ((Episode episode, _) in introFingerprints)
                    {
                        if (episodesWithIntro.Contains(item: episode.Id))
                            continue;

                        newSegments.Add(
                            item: new()
                            {
                                EpisodeId = episode.Id,
                                SegmentType = ContentSegmentType.Intro,
                                StartSeconds = marker.Start.TotalSeconds,
                                EndSeconds = marker.End.TotalSeconds,
                                Confidence = marker.Confidence,
                                Source = "detector",
                            }
                        );
                    }
                }
            }
        }

        if (needOutro)
        {
            List<(Episode Episode, AudioFingerprint Fp)> outroFingerprints = await FingerprintAsync(
                episodeFiles: episodeFiles,
                window: OutroWindow,
                ct: ct
            );

            if (outroFingerprints.Count >= 2)
            {
                IntroMarker? marker = _introDetector.DetectOutro(
                    episodeFingerprints: outroFingerprints.Select(selector: x => x.Fp).ToList()
                );

                if (marker is not null)
                {
                    foreach ((Episode episode, _) in outroFingerprints)
                    {
                        if (episodesWithOutro.Contains(item: episode.Id))
                            continue;

                        newSegments.Add(
                            item: new()
                            {
                                EpisodeId = episode.Id,
                                SegmentType = ContentSegmentType.Outro,
                                StartSeconds = marker.Start.TotalSeconds,
                                EndSeconds = marker.End.TotalSeconds,
                                Confidence = marker.Confidence,
                                Source = "detector",
                            }
                        );
                    }
                }
            }
        }

        if (newSegments.Count == 0)
            return;

        await using (MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct))
        {
            context.ContentSegments.AddRange(entities: newSegments);
            await context.SaveChangesAsync(cancellationToken: ct);
        }

        _logger.LogInformation(
            message: "IntroDetect: persisted {Count} segment(s) for season {SeasonId}", args: [newSegments.Count, seasonId]
        );
    }

    private async Task<List<(Episode Episode, string FilePath)>> ResolveFilePathsAsync(
        List<Episode> episodes,
        CancellationToken ct
    )
    {
        List<int> episodeIds = episodes.Select(selector: e => e.Id).ToList();

        List<(int EpisodeId, string HostFolder, string Filename)> fileRows;
        await using (MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct))
        {
            fileRows = await context
                .VideoFiles.AsNoTracking()
                .Where(predicate: vf => vf.EpisodeId != null && episodeIds.Contains(vf.EpisodeId!.Value))
                .Select(selector: vf => new ValueTuple<int, string, string>(
                    vf.EpisodeId!.Value,
                    vf.HostFolder,
                    vf.Filename
                ))
                .ToListAsync(cancellationToken: ct);
        }

        Dictionary<int, string> fileByEpisode = fileRows.ToDictionary(
            keySelector: r => r.EpisodeId,
            elementSelector: r => r.HostFolder + r.Filename
        );

        return episodes
            .Where(predicate: e => fileByEpisode.ContainsKey(key: e.Id))
            .Select(selector: e => (e, fileByEpisode[key: e.Id]))
            .ToList();
    }

    private async Task<List<(Episode Episode, AudioFingerprint Fp)>> FingerprintAsync(
        List<(Episode Episode, string FilePath)> episodeFiles,
        FingerprintWindow window,
        CancellationToken ct
    )
    {
        List<(Episode Episode, AudioFingerprint Fp)> results = [];

        foreach ((Episode episode, string filePath) in episodeFiles)
        {
            if (ct.IsCancellationRequested)
                break;

            if (!_storage.Exists(path: filePath))
            {
                _logger.LogDebug(
                    message: "IntroDetect: file not found for episode {EpisodeId}: {FilePath}", args: [episode.Id, filePath]
                );
                continue;
            }

            try
            {
                AudioFingerprint fp = await _fingerprinter.FingerprintAsync(filePath: filePath, window: window, ct: ct);
                results.Add(item: (episode, fp));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    exception: ex,
                    message: "IntroDetect: fingerprinting failed for episode {EpisodeId}",
                    args: episode.Id
                );
            }
        }

        return results;
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();
    }
}
