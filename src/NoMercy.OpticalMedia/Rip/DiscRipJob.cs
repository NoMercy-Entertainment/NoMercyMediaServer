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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Encoder.Audio;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.Events.FileWatcher;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.OpticalMedia.Rip;

/// <summary>
/// Durable queue job that rips one or more titles off an optical disc,
/// optionally auto-resolves metadata via TMDB, and moves the ripped file
/// into the target library folder, then fires a <see cref="FileCreatedEvent"/>
/// so the normal folder-watcher import pipeline picks it up.
///
/// Replaces the controller's <c>_ = Task.Run(...)</c> fire-and-forget approach.
/// One rip per drive is enforced via <see cref="DriveLockRegistry"/>. Progress
/// and completion are broadcast on the ripper hub via the event bus.
/// </summary>
[Serializable]
public class DiscRipJob : IShouldQueue, IJobStorageInjector
{
    [JsonIgnore]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [JsonIgnore]
    private ILogger Log => field ??= LoggerFactory.CreateLogger(type: GetType());

    public string QueueName => "import";
    public int Priority => 5;

    // ── Serialized payload (survives queue-DB round-trip) ────────────────

    public string JobId { get; init; } = Guid.NewGuid().ToString(format: "N");

    public RipRequest Request { get; set; } = null!;
    public string OutputDir { get; set; } = string.Empty;
    public Ulid? TargetFolderId { get; set; }
    public Ulid? TargetLibraryId { get; set; }
    public string? TargetLibraryType { get; set; }

    // ── Injected services (never serialized) ─────────────────────────────

    [JsonIgnore]
    public IDiscRipper DiscRipper { get; set; } = null!;

    [JsonIgnore]
    public DiscIdentificationService IdentificationService { get; set; } = null!;

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageDriver StorageDriver { get; set; } = null!;

    [JsonIgnore]
    public DriveLockRegistry DriveLockRegistry { get; set; } = null!;

    [JsonIgnore]
    public IAudioMetadataWriter AudioMetadataWriter { get; set; } = null!;

    [JsonIgnore]
    public MusicBrainzReleaseClient MusicBrainzReleaseClient { get; set; } = null!;

    /// <summary>
    /// Seam for dispatching follow-up encode jobs. Injected from DI so tests
    /// can supply a mock without touching the static QueueRunner.
    /// Falls back to <see cref="QueueRunner.Current"/>?.Dispatcher at runtime.
    /// </summary>
    [JsonIgnore]
    public IJobDispatcher? JobDispatcher { get; set; }

    // ── Constructors ─────────────────────────────────────────────────────

    public DiscRipJob() { }

    public DiscRipJob(
        RipRequest request,
        string outputDir,
        Ulid? targetFolderId,
        Ulid? targetLibraryId,
        string? targetLibraryType
    )
    {
        Request = request;
        OutputDir = outputDir;
        TargetFolderId = targetFolderId;
        TargetLibraryId = targetLibraryId;
        TargetLibraryType = targetLibraryType;
    }

    // ── IJobStorageInjector ───────────────────────────────────────────────

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        DiscRipper = serviceProvider.GetRequiredService<IDiscRipper>();
        IdentificationService = serviceProvider.GetRequiredService<DiscIdentificationService>();
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        StorageDriver = serviceProvider.GetRequiredService<IStorageDriver>();
        DriveLockRegistry = serviceProvider.GetRequiredService<DriveLockRegistry>();
        AudioMetadataWriter = serviceProvider.GetRequiredService<IAudioMetadataWriter>();
        // MusicBrainzReleaseClient is not a registered DI service; it is
        // constructed on demand everywhere else (ReleaseManager,
        // FileRepository, AudioImportJob). Resolving it from the provider
        // threw at activation, so construct it directly like the rest.
        MusicBrainzReleaseClient = new();
        JobDispatcher = QueueRunner.Current?.Dispatcher;
    }

    // ── IShouldQueue ──────────────────────────────────────────────────────

    public async Task Handle()
    {
        PublishProgress(status: "started", message: "Rip started");

        DiscRipResult[] results;
        try
        {
            results = await DiscRipper.RipAsync(request: Request, outputDirectory: OutputDir, ct: CancellationToken.None);
        }
        catch (DiscDriveBusyException)
        {
            PublishProgress(status: "error", message: "Drive is already in use by another rip job");
            Log.LogWarning(
                message: "[DiscRipJob] Drive {Drive} is busy — job {JobId} rejected", args: [Request.DrivePath, JobId]
            );
            return;
        }
        catch (Exception ex)
        {
            PublishProgress(status: "error", message: $"Rip failed: {ex.Message}");
            Log.LogError(exception: ex, message: "[DiscRipJob] Rip failed for drive {Drive}", args: Request.DrivePath);
            return;
        }

        bool shouldMove =
            Request.Mode == RipMode.RipAndEncode
            && TargetFolderId.HasValue
            && TargetLibraryId.HasValue
            && !string.IsNullOrEmpty(value: TargetLibraryType);

        if (!shouldMove)
        {
            PublishProgress(status: "complete", message: "Rip complete (raw files retained in output directory)");
            return;
        }

        // ── CD music tagging branch ───────────────────────────────────────
        // For audio CDs with an identified MusicBrainz release, tag each
        // ripped FLAC before the import pipeline sees them. Once tagged,
        // AudioImportJob can import them normally (it skips untagged files).
        // The MetadataId on the request carries the chosen release MBID.
        if (Request.DiscType == OpticalDiscType.Cd && !string.IsNullOrEmpty(value: Request.MetadataId))
        {
            await TagCdTracksAsync(results: results, releaseMbid: Request.MetadataId, ct: CancellationToken.None);
        }

        (Folder? targetFolder, Library? targetLibrary) = await FetchTargetsAsync(
            folderId: TargetFolderId!.Value,
            libraryId: TargetLibraryId!.Value,
            cancellationToken: CancellationToken.None
        );

        if (targetFolder is null || targetLibrary is null)
        {
            PublishProgress(
                status: "error",
                message: "Destination folder or library no longer exists — rip output left in output directory"
            );
            Log.LogWarning(
                message: "[DiscRipJob] Target folder {FolderId} or library {LibraryId} not found after rip", args: [TargetFolderId, TargetLibraryId]
            );
            return;
        }

        // When the caller supplied no custom metadata, build a synthetic DiscInfo
        // from the drive path label so the metadata resolver can attempt TMDB
        // auto-resolution. The probe already ran at rip time; we don't re-probe.
        DiscInfo? discInfo = null;
        if (Request.Custom is null)
        {
            // Separator-agnostic leaf so the synthetic label is identical on
            // Windows and Linux (Path.GetFileName treats '\' as literal on Linux).
            string trimmedDrivePath = Request.DrivePath.TrimEnd(trimChars: ['/', '\\']);
            string label = trimmedDrivePath[(trimmedDrivePath.LastIndexOfAny(anyOf: ['\\', '/']) + 1)..];
            discInfo = new(Type: Request.DiscType, DiscLabel: label, Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);
        }

        IStorage folderStorage = StorageFactory.For(
            folderId: targetFolder.Id,
            driverId: targetFolder.DriverId,
            subPath: string.Empty
        );

        DiscRipResult[] successes = results.Where(predicate: r => r.Success).ToArray();
        HashSet<string> notifiedFolders = new(comparer: StringComparer.OrdinalIgnoreCase);
        HashSet<int> dispatchedTitleIndexes = [];
        int batchIndex = 0;

        foreach (DiscRipResult res in successes)
        {
            RipRequest effectiveRequest = Request;

            if (Request.Custom is null && discInfo is not null)
            {
                DiscIdentification identification = await IdentificationService.IdentifyAsync(
                    disc: discInfo,
                    ct: CancellationToken.None
                );
                DiscCandidate? top = identification.TopCandidate;

                if (top is not null && identification.AutoApply)
                {
                    effectiveRequest = Request with
                    {
                        Custom = new(
                            Title: top.Title,
                            Year: top.Year,
                            Type: top.Type == MediaType.Movie ? MediaType.Movie : MediaType.TvShow,
                            PosterUrl: top.PosterUrl,
                            SeasonNumber: top.SeasonNumber,
                            EpisodeStartNumber: top.EpisodeNumber
                        ),
                    };
                }
                else if (top is not null)
                {
                    string pendingPath = Path.Combine(
                        path1: OutputDir,
                        path2: $"pending_{res.TitleIndex:D2}.json"
                    );
                    DiscRipPendingState pendingState = new(
                        RipOutputPath: res.OutputPath,
                        TitleIndex: res.TitleIndex,
                        DrivePath: Request.DrivePath,
                        DiscDurationSec: discInfo.MainTitleDurationSec,
                        Candidates: identification.Candidates.Take(count: 5).ToArray(),
                        CreatedAt: DateTimeOffset.UtcNow
                    );
                    await File.WriteAllTextAsync(
                        path: pendingPath,
                        contents: System.Text.Json.JsonSerializer.Serialize(
                            value: pendingState,
                            options: new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                        ),
                        cancellationToken: CancellationToken.None
                    );
                    PublishProgress(
                        status: "pending",
                        message: $"Title {res.TitleIndex} needs manual identification — saved to {pendingPath}"
                    );
                    continue;
                }
            }

            // CD rips produce files with their final name already embedded
            // in the output path (NN - Title.flac); use the filename directly
            // rather than the video-oriented RipOutputPathHelper.
            string folderRelative =
                Request.DiscType == OpticalDiscType.Cd
                    ? Path.GetFileName(path: res.OutputPath)
                    : RipOutputPathHelper.Build(
                        request: effectiveRequest,
                        libraryType: TargetLibraryType!,
                        titleIndex: res.TitleIndex,
                        batchIndex: batchIndex
                    );
            batchIndex++;

            string parentRelative = ParentRelative(folderRelative: folderRelative);
            if (!string.IsNullOrEmpty(value: parentRelative))
                await folderStorage.CreateDirectoryAsync(path: parentRelative, ct: CancellationToken.None);

            await using (FileStream src = new(path: res.OutputPath, mode: FileMode.Open, access: FileAccess.Read))
            await using (
                Stream dst = await folderStorage.OpenWriteAsync(
                    path: folderRelative,
                    overwrite: true,
                    ct: CancellationToken.None
                )
            )
            {
                await src.CopyToAsync(destination: dst, cancellationToken: CancellationToken.None);
            }

            string destinationHostPath = ResolveHostPath(storage: folderStorage, subPath: folderRelative);
            string watcherFolderHost = ResolveHostPath(storage: folderStorage, subPath: parentRelative);

            bool isVideoDisc =
                Request.DiscType == OpticalDiscType.Dvd
                || Request.DiscType == OpticalDiscType.BluRay;

            // Per-title dispatch: each ripped title is its own episode/file and
            // needs its own VideoEncodeJob. Gating this on the shared season
            // folder (like the audio branch below) meant only the first title
            // per folder ever dispatched — every other episode landed on disk
            // with no encode job and no fallback event.
            if (isVideoDisc && dispatchedTitleIndexes.Add(item: res.TitleIndex))
            {
                Ulid? resolvedPresetId = ResolvePresetId(
                    encodingProfileId: Request.EncodingProfileId,
                    presetFolders: targetFolder.EncodingPresetFolders
                );

                Log.LogInformation(
                    message: "[DiscRipJob] Dispatching VideoEncodeJob for {File} — preset {PresetId}", args: [destinationHostPath, resolvedPresetId.HasValue ? resolvedPresetId.Value.ToString() : "folder-default"]
                );

                IJobDispatcher? dispatcher = JobDispatcher;
                if (dispatcher is not null)
                {
                    VideoEncodeJob encodeJob = new()
                    {
                        LibraryId = targetLibrary.Id,
                        FolderId = targetFolder.Id,
                        InputFile = destinationHostPath,
                        PresetId = resolvedPresetId,
                    };
                    dispatcher.Dispatch(job: encodeJob, onQueue: encodeJob.QueueName, priority: encodeJob.Priority);
                }
                else
                {
                    Log.LogWarning(
                        message: "[DiscRipJob] JobDispatcher is null — falling back to FileCreatedEvent for {File}",
                        args: destinationHostPath
                    );
                    if (EventBusProvider.IsConfigured)
                    {
                        await EventBusProvider.Current.PublishAsync(
                            @event: new FileCreatedEvent
                            {
                                FolderPath = watcherFolderHost,
                                LibraryId = targetLibrary.Id,
                                LibraryType = targetLibrary.Type,
                            }
                        );
                    }
                }
            }
            else if (!isVideoDisc && notifiedFolders.Add(item: watcherFolderHost))
            {
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        @event: new FileCreatedEvent
                        {
                            FolderPath = watcherFolderHost,
                            LibraryId = targetLibrary.Id,
                            LibraryType = targetLibrary.Type,
                        }
                    );
                }
            }

            try
            {
                File.Delete(path: res.OutputPath);
            }
            catch
            {
                // best effort
            }

            Log.LogInformation(
                message: "[DiscRipJob] Title {Index} moved to {Dest}", args: [res.TitleIndex, folderRelative]
            );
        }

        int failCount = results.Count(predicate: r => !r.Success);
        string summary =
            failCount == 0
                ? $"Rip complete — {successes.Length} title(s) imported"
                : $"Rip complete — {successes.Length} succeeded, {failCount} failed";

        PublishProgress(status: "complete", message: summary);
    }

    // ── CD music tagging ─────────────────────────────────────────────────

    /// <summary>
    /// Re-fetches the full MusicBrainz release and writes ID3/Vorbis tags
    /// into each successfully ripped FLAC so AudioImportJob can import them.
    ///
    /// For a CD with no identified release (MetadataId is null/empty) this
    /// method is not called — the FLACs remain untagged and will not be
    /// auto-imported until the user assigns a release.
    /// </summary>
    private async Task TagCdTracksAsync(
        DiscRipResult[] results,
        string releaseMbid,
        CancellationToken ct
    )
    {
        MusicBrainzReleaseAppends? release = null;
        try
        {
            release = await MusicBrainzReleaseClient.WithAllAppends(
                id: Guid.Parse(input: releaseMbid),
                priority: false
            );
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                exception: ex,
                message: "[DiscRipJob] MusicBrainz release fetch failed for {Mbid}: {Message}", args: [releaseMbid, ex.Message]
            );
        }

        if (release is null)
        {
            Log.LogWarning(
                message: "[DiscRipJob] Release {Mbid} not found — FLAC files will be untagged",
                args: releaseMbid
            );
            return;
        }

        string albumArtist = FormatArtistCredit(credits: release.ArtistCredit);
        string albumTitle = release.Title;
        int? releaseYear = release.DateTime?.Year;
        string? releaseId = release.Id.ToString();

        // Fetch cover art URL from the release's Cover Art Archive entry.
        string? coverUrl = null;
        if (release.CoverArtArchive is { Front: true })
        {
            coverUrl = $"https://coverartarchive.org/release/{release.Id}/front";
        }

        AlbumArtSource? coverSource = coverUrl is not null
            ? new AlbumArtSource(FilePath: null, Url: coverUrl, Type: AlbumArtType.Front)
            : null;

        // Pick the first CD medium whose track count matches the number of
        // ripped tracks (mirrors AudioCdIdentifier.BuildTrackMappings logic).
        MusicBrainzMedia? medium = release.Media.FirstOrDefault(predicate: m =>
            m.TrackCount == results.Length || m.Tracks.Length == results.Length
        );

        if (medium is null && release.Media.Length > 0)
            medium = release.Media[0];

        foreach (DiscRipResult res in results.Where(predicate: r => r.Success))
        {
            ct.ThrowIfCancellationRequested();

            MusicBrainzTrack? mbTrack = medium?.Tracks.FirstOrDefault(predicate: t =>
                t.Position == res.TitleIndex
            );

            string trackTitle = mbTrack?.Title ?? $"Track {res.TitleIndex:D2}";
            string trackArtist = mbTrack is not null
                ? FormatArtistCredit(credits: mbTrack.ArtistCredit)
                : albumArtist;
            string? recordingMbid = mbTrack?.Recording.Id.ToString();

            string? genre = release.Genres is { Length: > 0 } ? release.Genres[0].Name : null;

            AudioMetadata metadata = new(
                Title: trackTitle,
                Artist: trackArtist,
                AlbumArtist: albumArtist,
                Album: albumTitle,
                TrackNumber: res.TitleIndex,
                DiscNumber: 1,
                Year: releaseYear,
                Genre: genre,
                MusicBrainzTrackId: recordingMbid,
                MusicBrainzReleaseId: releaseId,
                AcoustIdFingerprint: null,
                CoverArt: coverSource
            );

            try
            {
                await AudioMetadataWriter.WriteTagsAsync(filePath: res.OutputPath, metadata: metadata, ct: ct);
                Log.LogInformation(
                    message: "[DiscRipJob] Tagged {Path} — {Artist} / {Title}", args: [res.OutputPath, trackArtist, trackTitle]
                );
            }
            catch (Exception ex)
            {
                Log.LogWarning(
                    exception: ex,
                    message: "[DiscRipJob] Tag write failed for {Path}: {Message}", args: [res.OutputPath, ex.Message]
                );
            }
        }
    }

    private static string FormatArtistCredit(ReleaseArtistCredit[] credits) =>
        string.Concat(values: credits.Select(selector: credit => (credit.Name ?? string.Empty) + credit.Joinphrase));

    // ── DB fetch (virtual for test override) ─────────────────────────────

    protected virtual async Task<(Folder? Folder, Library? Library)> FetchTargetsAsync(
        Ulid folderId,
        Ulid libraryId,
        CancellationToken cancellationToken
    )
    {
        await using MediaContext db = new();

        Folder? folder = await db
            .Folders.AsNoTracking()
            .Include(navigationPropertyPath: f => f.EncodingPresetFolders)
                .ThenInclude(navigationPropertyPath: link => link.Preset)
            .FirstOrDefaultAsync(predicate: f => f.Id == folderId, cancellationToken: cancellationToken);

        Library? library = await db
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: l => l.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
            .FirstOrDefaultAsync(predicate: l => l.Id == libraryId, cancellationToken: cancellationToken);

        return (folder, library);
    }

    // ── Preset resolution ────────────────────────────────────────────────

    private static Ulid? ResolvePresetId(
        string? encodingProfileId,
        IEnumerable<Database.Models.Media.EncodingPresetFolder> presetFolders
    )
    {
        if (string.IsNullOrEmpty(value: encodingProfileId))
            return null;

        if (!Ulid.TryParse(base32: encodingProfileId, ulid: out Ulid requested))
            return null;

        bool matched = presetFolders.Any(predicate: link =>
            link.Preset is not null && link.Preset.Id == requested
        );

        return matched ? requested : null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void PublishProgress(string status, string message)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        string methodName = status switch
        {
            "started" => "rip_started",
            "complete" => "rip_complete",
            "error" => "rip_error",
            "pending" => "rip_pending",
            _ => "rip_progress",
        };

        _ = EventBusProvider.Current.PublishAsync(
            @event: new DriveStateChangedEvent
            {
                DriveStateData = new(
                    Method: methodName,
                    Drive: Request?.DrivePath ?? string.Empty,
                    VolumeLabel: null,
                    HasDisc: true,
                    DiscType: (Request?.DiscType.ToString() ?? "none").ToLowerInvariant(),
                    Timestamp: DateTime.UtcNow,
                    JobId: JobId,
                    Message: message
                ),
            }
        );
    }

    private static string ParentRelative(string folderRelative)
    {
        int slash = folderRelative.LastIndexOf(value: '/');
        return slash <= 0 ? "" : folderRelative[..slash];
    }

    private static string ResolveHostPath(IStorage storage, string subPath)
    {
        try
        {
            return storage.GetFullPath(path: subPath);
        }
        catch
        {
            return subPath;
        }
    }
}
