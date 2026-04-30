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
    private readonly List<IDisposable> _subscriptions = [];

    // Fingerprint the first 3 minutes for intro detection.
    private static readonly FingerprintWindow IntroWindow = new(
        TimeSpan.Zero,
        TimeSpan.FromMinutes(3)
    );

    // Fingerprint the last 4 minutes for outro detection.
    private static readonly FingerprintWindow OutroWindow = new(
        TimeSpan.FromMinutes(-4),
        TimeSpan.FromMinutes(4)
    );

    public IntroDetectSubscriber(
        IEventBus eventBus,
        IAudioFingerprinter fingerprinter,
        IIntroDetector introDetector,
        EncoderOptions options,
        ILogger<IntroDetectSubscriber> logger,
        IStorage storage
    )
    {
        _fingerprinter = fingerprinter;
        _introDetector = introDetector;
        _options = options;
        _logger = logger;
        _storage = storage;

        _subscriptions.Add(eventBus.Subscribe<LibraryScanCompletedEvent>(OnLibraryScanCompleted));
    }

    internal async Task OnLibraryScanCompleted(
        LibraryScanCompletedEvent @event,
        CancellationToken ct
    )
    {
        if (!_options.EnableIntroDetectSubscriber)
            return;

        _logger.LogInformation(
            "IntroDetect: starting for library {LibraryName} ({LibraryId})",
            @event.LibraryName,
            @event.LibraryId
        );

        // Load all TV seasons whose show belongs to this library.
        List<int> seasonIds;
        await using (MediaContext context = new())
        {
            seasonIds = await context
                .Seasons.AsNoTracking()
                .Where(s =>
                    context.LibraryTv.Any(lt =>
                        lt.LibraryId == @event.LibraryId && lt.TvId == s.TvId
                    )
                )
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        foreach (int seasonId in seasonIds)
        {
            if (ct.IsCancellationRequested)
                break;

            await ProcessSeasonAsync(seasonId, ct);
        }
    }

    private async Task ProcessSeasonAsync(int seasonId, CancellationToken ct)
    {
        List<Episode> episodes;
        HashSet<int> episodesWithManualIntro;
        HashSet<int> episodesWithManualOutro;

        await using (MediaContext context = new())
        {
            episodes = await context
                .Episodes.AsNoTracking()
                .Where(e => e.SeasonId == seasonId)
                .OrderBy(e => e.EpisodeNumber)
                .ToListAsync(ct);

            // Two-step to avoid SQLite APPLY: fetch episode IDs with manual segments first.
            List<int> episodeIds = episodes.Select(e => e.Id).ToList();

            List<ContentSegment> manualSegments = await context
                .ContentSegments.AsNoTracking()
                .Where(cs =>
                    cs.EpisodeId != null
                    && episodeIds.Contains(cs.EpisodeId!.Value)
                    && cs.Source == "manual"
                )
                .ToListAsync(ct);

            episodesWithManualIntro = manualSegments
                .Where(cs => cs.SegmentType == ContentSegmentType.Intro)
                .Select(cs => cs.EpisodeId!.Value)
                .ToHashSet();

            episodesWithManualOutro = manualSegments
                .Where(cs => cs.SegmentType == ContentSegmentType.Outro)
                .Select(cs => cs.EpisodeId!.Value)
                .ToHashSet();
        }

        if (episodes.Count < 2)
        {
            _logger.LogDebug(
                "IntroDetect: season {SeasonId} has fewer than 2 episodes — skipping",
                seasonId
            );
            return;
        }

        // Resolve source file paths for each episode.
        List<(Episode Episode, string FilePath)> episodeFiles = await ResolveFilePathsAsync(
            episodes,
            ct
        );

        if (episodeFiles.Count < 2)
            return;

        // Fingerprint intro windows.
        bool needIntro = episodeFiles.Any(ef => !episodesWithManualIntro.Contains(ef.Episode.Id));
        bool needOutro = episodeFiles.Any(ef => !episodesWithManualOutro.Contains(ef.Episode.Id));

        if (!needIntro && !needOutro)
        {
            _logger.LogDebug(
                "IntroDetect: all episodes in season {SeasonId} already have manual segments",
                seasonId
            );
            return;
        }

        List<ContentSegment> newSegments = [];

        if (needIntro)
        {
            List<(Episode Episode, AudioFingerprint Fp)> introFingerprints = await FingerprintAsync(
                episodeFiles,
                IntroWindow,
                ct
            );

            if (introFingerprints.Count >= 2)
            {
                IntroMarker? marker = _introDetector.DetectIntro(
                    introFingerprints.Select(x => x.Fp).ToList()
                );

                if (marker is not null)
                {
                    foreach ((Episode episode, _) in introFingerprints)
                    {
                        if (episodesWithManualIntro.Contains(episode.Id))
                            continue;

                        newSegments.Add(
                            new ContentSegment
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
                episodeFiles,
                OutroWindow,
                ct
            );

            if (outroFingerprints.Count >= 2)
            {
                IntroMarker? marker = _introDetector.DetectOutro(
                    outroFingerprints.Select(x => x.Fp).ToList()
                );

                if (marker is not null)
                {
                    foreach ((Episode episode, _) in outroFingerprints)
                    {
                        if (episodesWithManualOutro.Contains(episode.Id))
                            continue;

                        newSegments.Add(
                            new ContentSegment
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

        await using (MediaContext context = new())
        {
            context.ContentSegments.AddRange(newSegments);
            await context.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "IntroDetect: persisted {Count} segment(s) for season {SeasonId}",
            newSegments.Count,
            seasonId
        );
    }

    private async Task<List<(Episode Episode, string FilePath)>> ResolveFilePathsAsync(
        List<Episode> episodes,
        CancellationToken ct
    )
    {
        List<int> episodeIds = episodes.Select(e => e.Id).ToList();

        List<(int EpisodeId, string HostFolder, string Filename)> fileRows;
        await using (MediaContext context = new())
        {
            fileRows = await context
                .VideoFiles.AsNoTracking()
                .Where(vf => vf.EpisodeId != null && episodeIds.Contains(vf.EpisodeId!.Value))
                .Select(vf => new ValueTuple<int, string, string>(
                    vf.EpisodeId!.Value,
                    vf.HostFolder,
                    vf.Filename
                ))
                .ToListAsync(ct);
        }

        Dictionary<int, string> fileByEpisode = fileRows.ToDictionary(
            r => r.EpisodeId,
            r => r.HostFolder + r.Filename
        );

        return episodes
            .Where(e => fileByEpisode.ContainsKey(e.Id))
            .Select(e => (e, fileByEpisode[e.Id]))
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

            if (!_storage.Exists(filePath))
            {
                _logger.LogDebug(
                    "IntroDetect: file not found for episode {EpisodeId}: {FilePath}",
                    episode.Id,
                    filePath
                );
                continue;
            }

            try
            {
                AudioFingerprint fp = await _fingerprinter.FingerprintAsync(filePath, window, ct);
                results.Add((episode, fp));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "IntroDetect: fingerprinting failed for episode {EpisodeId}",
                    episode.Id
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
