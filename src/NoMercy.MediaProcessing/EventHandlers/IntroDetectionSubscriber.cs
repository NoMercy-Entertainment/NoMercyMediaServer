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

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.EventHandlers;

/// <summary>
/// Wires <see cref="EncodingCompletedEvent"/> to the chromaprint-based
/// intro/outro detector and persists the results as
/// <see cref="ContentSegment"/> rows so the player can render "Skip Intro"
/// buttons. Runs in the background — fingerprinting is ffmpeg-heavy, so the
/// subscriber queues work on a <see cref="Task.Run"/> to keep the event
/// bus callback cheap.
///
/// Detection only fires once a season has at least <see cref="MinEpisodes"/>
/// encoded episodes. Any fewer and there isn't enough cross-episode data
/// to pick out a shared opening/closing theme reliably.
/// </summary>
public class IntroDetectionSubscriber(
    IEventBus eventBus,
    IServiceScopeFactory scopeFactory,
    ILogger<IntroDetectionSubscriber> logger,
    IStorage storage
) : IHostedService
{
    private const int MinEpisodes = 3;
    private static readonly TimeSpan IntroScanWindow = TimeSpan.FromMinutes(minutes: 3);
    private static readonly TimeSpan OutroScanWindow = TimeSpan.FromMinutes(minutes: 3);

    private readonly ConcurrentDictionary<int, byte> _seasonsInFlight = new();
    private readonly List<IDisposable> _subscriptions = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriptions.Add(
            item: eventBus.Subscribe<EncodingCompletedEvent>(
                handler: (evt, ct) =>
                {
                    _ = Task.Run(function: () => OnEncodingCompletedAsync(evt: evt, ct: ct), cancellationToken: ct);
                    return Task.CompletedTask;
                }
            )
        );

        logger.LogInformation(message: "Intro detection subscriber active");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(exception: ex, message: "Could not dispose intro detection subscription");
            }
        }
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    private async Task OnEncodingCompletedAsync(EncodingCompletedEvent evt, CancellationToken ct)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            MediaContext context = scope.ServiceProvider.GetRequiredService<MediaContext>();

            Episode? episode = await context
                .Episodes.AsNoTracking()
                .Include(navigationPropertyPath: e => e.VideoFiles)
                .FirstOrDefaultAsync(predicate: e => e.Id == evt.JobId, cancellationToken: ct);

            if (episode is null)
                return; // Not an episode — movies don't have cross-episode intro matching yet.

            if (!_seasonsInFlight.TryAdd(key: episode.SeasonId, value: 0))
                return; // Another encode in the same season is already being processed.

            try
            {
                await DetectAndPersistForSeasonAsync(services: scope.ServiceProvider, seasonId: episode.SeasonId, ct: ct);
            }
            finally
            {
                _seasonsInFlight.TryRemove(key: episode.SeasonId, value: out _);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(exception: ex, message: "Intro detection failed for job {JobId}", args: evt.JobId);
        }
    }

    private async Task DetectAndPersistForSeasonAsync(
        IServiceProvider services,
        int seasonId,
        CancellationToken ct
    )
    {
        MediaContext context = services.GetRequiredService<MediaContext>();
        List<Episode> encodedEpisodes = await context
            .Episodes.AsNoTracking()
            .Include(navigationPropertyPath: e => e.VideoFiles)
            .Where(predicate: e => e.SeasonId == seasonId && e.VideoFiles.Count > 0)
            .OrderBy(keySelector: e => e.EpisodeNumber)
            .ToListAsync(cancellationToken: ct);

        if (encodedEpisodes.Count < MinEpisodes)
        {
            logger.LogDebug(
                message: "Season {SeasonId} only has {Count} encoded episodes — skipping (need {Min})", args: [seasonId, encodedEpisodes.Count, MinEpisodes]
            );
            return;
        }

        IAudioFingerprinter fingerprinter = services.GetRequiredService<IAudioFingerprinter>();
        IIntroDetector detector = services.GetRequiredService<IIntroDetector>();

        Dictionary<int, (AudioFingerprint Intro, AudioFingerprint Outro)> fingerprints = new();

        foreach (Episode episode in encodedEpisodes)
        {
            ct.ThrowIfCancellationRequested();

            string? inputPath = ResolveEpisodeInputPath(episode: episode);
            if (inputPath is null)
                continue;

            try
            {
                AudioFingerprint introPrint = await fingerprinter.FingerprintAsync(
                    filePath: inputPath,
                    window: new(Start: TimeSpan.Zero, Duration: IntroScanWindow),
                    ct: ct
                );

                TimeSpan sourceDuration = ParseDuration(file: episode.VideoFiles.FirstOrDefault());
                TimeSpan outroStart =
                    sourceDuration > OutroScanWindow
                        ? sourceDuration - OutroScanWindow
                        : TimeSpan.Zero;

                AudioFingerprint outroPrint = await fingerprinter.FingerprintAsync(
                    filePath: inputPath,
                    window: new(Start: outroStart, Duration: OutroScanWindow),
                    ct: ct
                );

                fingerprints[key: episode.Id] = (introPrint, outroPrint);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    exception: ex,
                    message: "Could not fingerprint episode {EpisodeId} — excluding from season scan",
                    args: episode.Id
                );
            }
        }

        if (fingerprints.Count < MinEpisodes)
            return;

        IReadOnlyList<AudioFingerprint> intros = fingerprints.Values.Select(selector: f => f.Intro).ToList();
        IReadOnlyList<AudioFingerprint> outros = fingerprints.Values.Select(selector: f => f.Outro).ToList();

        IntroMarker? introMarker = detector.DetectIntro(episodeFingerprints: intros);
        IntroMarker? outroMarker = detector.DetectOutro(episodeFingerprints: outros);

        foreach (int episodeId in fingerprints.Keys)
        {
            List<ContentSegment> segments = [];
            if (introMarker is not null)
            {
                segments.Add(
                    item: new()
                    {
                        SegmentType = ContentSegmentType.Intro,
                        StartSeconds = introMarker.Start.TotalSeconds,
                        EndSeconds = introMarker.End.TotalSeconds,
                        Confidence = introMarker.Confidence,
                    }
                );
            }
            if (outroMarker is not null)
            {
                segments.Add(
                    item: new()
                    {
                        SegmentType = ContentSegmentType.Outro,
                        StartSeconds = outroMarker.Start.TotalSeconds,
                        EndSeconds = outroMarker.End.TotalSeconds,
                        Confidence = outroMarker.Confidence,
                    }
                );
            }

            if (segments.Count > 0)
                await ReplaceDetectorSegmentsAsync(context: context, episodeId: episodeId, newSegments: segments, ct: ct);
        }

        logger.LogInformation(
            message: "Intro detection completed for season {SeasonId}: intro={HasIntro} outro={HasOutro} across {Count} episodes", args: [seasonId, introMarker is not null, outroMarker is not null, fingerprints.Count]
        );
    }

    /// <summary>
    /// Removes previously-written detector rows for this episode and writes
    /// the fresh set. Manual edits (Source != "detector") are left alone so
    /// user corrections survive re-detection.
    /// </summary>
    private static async Task ReplaceDetectorSegmentsAsync(
        MediaContext context,
        int episodeId,
        List<ContentSegment> newSegments,
        CancellationToken ct
    )
    {
        List<ContentSegment> old = await context
            .ContentSegments.Where(predicate: s => s.EpisodeId == episodeId && s.Source == "detector")
            .ToListAsync(cancellationToken: ct);

        context.ContentSegments.RemoveRange(entities: old);

        foreach (ContentSegment seg in newSegments)
        {
            seg.EpisodeId = episodeId;
            seg.MovieId = null;
            seg.Source = "detector";
            seg.CreatedAt = DateTime.UtcNow;
            seg.UpdatedAt = seg.CreatedAt;
            context.ContentSegments.Add(entity: seg);
        }

        await context.SaveChangesAsync(cancellationToken: ct);
    }

    private string? ResolveEpisodeInputPath(Episode episode)
    {
        // The subscriber fingerprints the ORIGINAL source (not HLS output)
        // so the timestamps it detects are meaningful against the full
        // timeline the player sees. Use the first non-empty VideoFile.
        foreach (VideoFile file in episode.VideoFiles)
        {
            if (
                string.IsNullOrWhiteSpace(value: file.HostFolder)
                || string.IsNullOrWhiteSpace(value: file.Filename)
            )
                continue;

            string path = storage.CombinePath(parent: file.HostFolder, child: file.Filename);
            if (storage.Exists(path: path))
                return path;
        }

        return null;
    }

    private static TimeSpan ParseDuration(VideoFile? file)
    {
        if (file is null || string.IsNullOrWhiteSpace(value: file.Duration))
            return TimeSpan.Zero;

        // VideoFile.Duration is stored as "HH:MM:SS" in the DB.
        if (TimeSpan.TryParse(s: file.Duration, result: out TimeSpan parsed))
            return parsed;

        return TimeSpan.Zero;
    }
}
