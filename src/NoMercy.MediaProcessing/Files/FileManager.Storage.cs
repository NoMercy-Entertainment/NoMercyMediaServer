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

using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Subtitles;
using NoMercy.MediaProcessing.Jobs.SubtitleJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using NoMercyQueue;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;
using QueueJobDispatcher = NoMercyQueue.JobDispatcher;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager
{
    private async Task StoreMusic()
    {
        List<MediaFile> items = Files
            .SelectMany(selector: file => file.Files ?? [])
            .Where(predicate: mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        if (items.Count == 0)
            return;

        foreach (MediaFile item in items)
            await StoreAudioItem(item: item);

        Logger.App(message: $"Found {items.Count} music files");
    }

    private async Task StoreMovie()
    {
        MediaFile? item = Files
            .SelectMany(selector: file => file.Files ?? [])
            .FirstOrDefault(predicate: file => file.Parsed is not null);

        if (item == null)
            return;

        await StoreVideoItem(item: item);

        Logger.App(message: $"Found {item.Path} for {Movie?.Title}");
    }

    private async Task StoreTvShow()
    {
        List<MediaFile> items = Files
            .SelectMany(selector: file => file.Files ?? [])
            .Where(predicate: mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        Logger.App(
            message: $"[StoreTvShow] {Show?.Title} ({Show?.Id}): {items.Count} candidate files across {Folders.Count} folder(s)",
            level: LogEventLevel.Information
        );

        if (items.Count == 0)
            return;

        int idx = 0;
        foreach (MediaFile item in items)
        {
            idx++;
            Logger.App(
                message: $"[StoreVideoItem] {idx}/{items.Count} {Path.GetFileName(path: item.Path)}",
                level: LogEventLevel.Information
            );
            await StoreVideoItem(item: item);
        }

        Logger.App(message: $"Found {items.Count} files for {Show?.Title}");
    }

    private async Task StoreAudioItem(MediaFile? item)
    {
        if (item?.Parsed is null)
            return;

        Folder? folder = Folders.FirstOrDefault(predicate: folder => item.Path.Contains(value: folder.Path));
        if (folder == null)
            return;

        await Task.CompletedTask;
    }

    private async Task StoreVideoItem(MediaFile item)
    {
        string itemPath = item.Path.Replace(oldChar: '\\', newChar: '/');
        Folder? folder = Folders.FirstOrDefault(predicate: f =>
            itemPath.Contains(value: f.Path.Replace(oldChar: '\\', newChar: '/'), comparisonType: StringComparison.OrdinalIgnoreCase)
        );
        if (folder == null)
        {
            Logger.App(
                message: $"[StoreVideoItem] no Folders match for {itemPath} — skipping (Folders={string.Join(separator: ", ", values: Folders.Select(selector: f => f.Path))})",
                level: LogEventLevel.Warning
            );
            return;
        }

        // MediaScan resolves every path through the driver, so item.Path is
        // driver-absolute (e.g. "/mnt/vault/Media/Marvels/TV.Shows/..."). Every
        // storage call below is on the IStorage facade, which rejects absolute
        // keys on remote backends (StoragePathGuard). Rebase the path onto its
        // scope-relative root — the folder's stored Path — so the facade, the
        // stored HostFolder/Folder, and the served URLs are all backend-neutral.
        itemPath = StoragePathHelpers.RebaseToFolderRoot(absolutePath: itemPath, folderPath: folder.Path);

        IStorage storage = StorageFor(folder: folder);
        string fileName = "/" + storage.GetName(path: itemPath);
        string hostFolder = itemPath.Replace(oldValue: fileName, newValue: "");
        string showName = (Movie?.Folder ?? Show?.Folder).OrEmpty().Trim(trimChars: ['/', '\\']);
        int showIdx = string.IsNullOrEmpty(value: showName)
            ? -1
            : itemPath.IndexOf(value: showName, comparisonType: StringComparison.OrdinalIgnoreCase);
        string baseFolder =
            showIdx >= 0 ? ("/" + itemPath[showIdx..]).Replace(oldValue: fileName, newValue: "") : hostFolder;

        List<Subtitle> subtitles = GetSubtitles(storage: storage, hostFolder: hostFolder);

        DispatchOrphanedBitmapOcrBackfill(storage: storage, folder: folder, hostFolder: hostFolder);

        List<VideoTrack> tracks = GetExtraFiles(storage: storage, hostFolder: hostFolder);

        Episode? episode = await fileRepository.GetEpisode(showId: Show?.Id, item: item);

        Metadata metadata = await MakeMetadata(
            storage: storage,
            item: item,
            fileName: fileName,
            baseFolder: baseFolder,
            hostFolder: hostFolder,
            extraFiles: tracks
        );

        Ulid metadataId = await fileRepository.StoreMetadata(metadata: metadata);

        Logger.App(message: $"Storing video file: {episode?.Id}, {Movie?.Id}", level: LogEventLevel.Verbose);

        // Joined multi-episode file (e.g. S01E01-E04): record the last covered
        // episode so every episode in the range counts as present, while a single
        // VideoFile keeps it from being listed twice in playlists.
        int? lastEpisodeNumber = null;
        if (episode is not null)
        {
            IReadOnlyList<int> covered = Parsing.EpisodeRangeParser.Expand(
                name: fileName,
                season: episode.SeasonNumber,
                firstEpisode: episode.EpisodeNumber
            );
            if (covered.Count > 1)
                lastEpisodeNumber = covered[^1];
        }
        VideoFile videoFile = new()
        {
            EpisodeId = episode?.Id,
            LastEpisodeNumber = lastEpisodeNumber,
            MovieId = Movie?.Id,
            Folder = baseFolder.Replace(oldValue: "\\", newValue: "/"),
            HostFolder = hostFolder.Replace(oldValue: "\\", newValue: "/"),
            Filename = fileName.Replace(oldValue: "\\", newValue: "/"),

            Share = folder.Id.ToString(),
            Duration = Regex.Replace(
                input: Regex.Replace(input: (item.FFprobe?.Duration.ToString()).OrEmpty(), pattern: @"\.\d+", replacement: ""),
                pattern: "^00:",
                replacement: ""
            ),
            // Chapters = JsonConvert.SerializeObject(item.FFprobe?.Chapters ?? []),
            Chapters = "",
            // Derive languages from the encoded audio tracks (the audio_<lang>_<codec>
            // dirs parsed into metadata.Audio), not the source ffprobe: a scan skips
            // ffprobe on remote drivers, so item.FFprobe is empty over NFS and this
            // column came out blank. The encoded tracks are the authoritative set of
            // playable languages anyway.
            Languages = JsonConvert.SerializeObject(
                value: (metadata.Audio ?? [])
                    .Select(selector: audio => audio.Language)
                    .Where(predicate: language => !string.IsNullOrEmpty(value: language) && language != "und")
                    .Distinct()
            ),
            Quality = (item.FFprobe?.VideoStreams.FirstOrDefault()?.Width.ToString()).OrEmpty(),
            Subtitles = JsonConvert.SerializeObject(value: subtitles),
            Tracks = tracks.ToArray(),
            MetadataId = metadataId,
        };

        await fileRepository.StoreVideoFile(videoFile: videoFile);
    }

    /// <summary>
    /// Queues an OCR backfill for every preserved bitmap subtitle
    /// (<c>.sup</c>/<c>.sub</c>) under <paramref name="hostFolder"/> that has no
    /// text sibling carrying the same <c>{lang}.{variant}</c>. A title encoded
    /// before the OCR pipeline worked keeps its bitmap sidecar but never got a
    /// <c>.vtt</c>, so clients that cannot render PGS show nothing. OCR is minutes
    /// of ffmpeg per track, far too slow for the scan thread, so the actual work
    /// runs as a low-priority <see cref="SubtitleOcrBackfillJob"/> on the encoder
    /// queue; the job is idempotent, so re-queuing across scans until it lands is
    /// harmless. A bitmap track that already has an <c>.ass</c>/<c>.srt</c>/<c>.vtt</c>
    /// is left alone — OCRing it would double the track in the picker.
    /// </summary>
    private static void DispatchOrphanedBitmapOcrBackfill(
        IStorage storage,
        Folder folder,
        string hostFolder
    )
    {
        string subtitleFolder = storage.CombinePath(parent: hostFolder, child: "subtitles");
        if (!storage.Exists(path: subtitleFolder))
            return;

        QueueJobDispatcher? dispatcher = QueueRunner.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        IReadOnlyList<OrphanedBitmapSubtitle> orphans = SelectOrphanedBitmapSubtitles(
            subtitleFileNames: storage
                .List(path: subtitleFolder, pattern: null, recursive: false)
                .Where(predicate: candidate => !candidate.IsDirectory)
                .Select(selector: candidate => storage.GetName(path: candidate.Path))
        );

        foreach ((string supName, string mediaTitle, string language, string variant) in orphans)
        {
            dispatcher.Dispatch(
                job: new SubtitleOcrBackfillJob(
                    folderId: folder.Id.ToString(),
                    driverId: folder.DriverId.ToString(),
                    hostFolder: hostFolder,
                    supFileName: supName,
                    mediaTitle: mediaTitle,
                    language: language,
                    variant: variant
                ),
                onQueue: "encoder",
                priority: 2
            );

            Logger.App(
                message: $"[SubtitleOcrBackfill] queued OCR for orphaned bitmap subtitle {supName}",
                level: LogEventLevel.Information
            );
        }
    }

    /// <summary>
    /// From a flat list of <c>subtitles/</c> filenames, returns each bitmap
    /// sidecar (<c>.sup</c>/<c>.sub</c>) that has no text track (<c>.vtt</c>,
    /// <c>.ass</c>, <c>.srt</c>, …) carrying the same <c>{lang}.{variant}</c>. A
    /// bitmap already backed by a text track is skipped so OCR never produces a
    /// duplicate track in the picker. The <c>MediaTitle</c> is the filename stem
    /// before <c>.{lang}.{variant}.{ext}</c>, reproduced on the OCR output so the
    /// scan pairs the new <c>.vtt</c> with its bitmap sibling.
    /// </summary>
    internal static IReadOnlyList<OrphanedBitmapSubtitle> SelectOrphanedBitmapSubtitles(
        IEnumerable<string> subtitleFileNames
    )
    {
        HashSet<string> textKeys = new(comparer: StringComparer.OrdinalIgnoreCase);
        List<OrphanedBitmapSubtitle> bitmaps = [];

        foreach (string name in subtitleFileNames)
        {
            Match match = SubtitleFileRegex().Match(input: name);
            if (!match.Success)
                continue;

            string language = match.Groups[groupname: "lang"].Value;
            string variant = match.Groups[groupname: "type"].Value;

            if (SubtitleClassifier.IsBitmapSidecarExtension(extension: match.Groups[groupname: "ext"].Value))
                bitmaps.Add(item: new(SupName: name, MediaTitle: name[..match.Index].TrimEnd(trimChar: '.'), Language: language, Variant: variant));
            else
                textKeys.Add(item: $"{language}|{variant}");
        }

        return bitmaps
            .Where(predicate: bitmap => !textKeys.Contains(item: $"{bitmap.Language}|{bitmap.Variant}"))
            .ToList();
    }

    private async Task<Metadata> MakeMetadata(
        IStorage storage,
        MediaFile item,
        string fileName,
        string baseFolder,
        string hostFolder,
        List<VideoTrack> extraFiles
    )
    {
        List<IVideo> video = GetVideoHashList(storage: storage, hostFolder: hostFolder);
        List<IAudio> audio = GetAudioHashList(storage: storage, hostFolder: hostFolder);
        List<ISubtitle> subtitles = GetSubtitleHashList(storage: storage, hostFolder: hostFolder);
        List<IFont> fonts = GetFontHashList(storage: storage, hostFolder: hostFolder);
        List<IPreview> previews = GetPreviewHashList(storage: storage, hostFolder: hostFolder, extraFiles: extraFiles);

        await RebuildHlsMasterFromDiskAsync(storage: storage, hostFolder: hostFolder, fileName: fileName, video: video);

        VideoTrack? chaptersFile = extraFiles.FirstOrDefault(predicate: file => file.Kind == "chapters");

        List<IChapter> chapters = chaptersFile?.File is not null
            ? await GetChapterHashListAsync(
                storage: storage,
                hostFolder: hostFolder,
                file: Path.GetFileName(path: chaptersFile.File)
            )
            : [];

        IChapterFile? chaptersFileHashMap = chaptersFile?.File is not null
            ? new()
            {
                FileName = "/" + Path.GetFileName(path: chaptersFile.File).Replace(oldValue: "\\", newValue: "/"),
                FileSize = storage.SizeOrZero(
                    path: storage.CombinePath(parent: hostFolder, child: Path.GetFileName(path: chaptersFile.File))
                ),
                FileHash = ComputeFileHash(
                    storage: storage,
                    filePath: storage.CombinePath(parent: hostFolder, child: Path.GetFileName(path: chaptersFile.File))
                ),
            }
            : null;

        IFontsFile? fontsFileHashMap = extraFiles
            .Where(predicate: file => file.Kind == "fonts")
            .Select(selector: file => new IFontsFile
            {
                FileName = "/" + Path.GetFileName(path: file.File).Replace(oldValue: "\\", newValue: "/"),
                FileSize = storage.SizeOrZero(
                    path: storage.CombinePath(parent: hostFolder, child: Path.GetFileName(path: file.File).OrEmpty())
                ),
                FileHash = ComputeFileHash(
                    storage: storage,
                    filePath: storage.CombinePath(parent: hostFolder, child: Path.GetFileName(path: file.File).OrEmpty())
                ),
            })
            .FirstOrDefault();

        Metadata metadata = new()
        {
            Filename = fileName.Replace(oldValue: "\\", newValue: "/"),
            Duration = (item.FFprobe?.Duration.ToString()).OrEmpty(),
            Folder = baseFolder.Replace(oldValue: "\\", newValue: "/"),
            HostFolder = hostFolder.Replace(oldValue: "\\", newValue: "/"),
            // Sum the sizes already gathered while building the hash lists rather
            // than walking every segment again: a rescan verifies completeness and
            // must not deep-dive the whole output tree over the network just to set
            // a storage-accounting stat. Covers every catalogued asset (video and
            // audio variants, subtitles, fonts, previews, chapters).
            FolderSize =
                video.Sum(selector: entry => entry.FileSize ?? 0)
                + audio.Sum(selector: entry => entry.FileSize ?? 0)
                + subtitles.Sum(selector: entry => entry.FileSize ?? 0)
                + fonts.Sum(selector: entry => entry.FileSize ?? 0)
                + previews.Sum(selector: entry => entry.ImageFileSize + entry.TimeFileSize)
                + (chaptersFileHashMap?.FileSize ?? 0)
                + (fontsFileHashMap?.FileSize ?? 0),

            Type = Movie?.Id is not null
                ? Database.Models.Media.MediaType.Movie
                : Database.Models.Media.MediaType.Tv,

            Audio = audio,
            Previews = previews,
            Subtitles = subtitles,
            Video = video,
            Chapters = chapters,
            ChapterFile = chaptersFileHashMap,
            Fonts = fonts,
            FontsFile = fontsFileHashMap,
        };

        return metadata;
    }

    /// <summary>
    /// Rebuilds <c>{masterTitle}.m3u8</c> from the renditions actually on disk
    /// under <paramref name="hostFolder"/> — the fully-published media root a
    /// scan walks, with none of the encode-finalize path's staging/scope
    /// fragility (a coordinator's finalize can resolve against a scope that
    /// only sees the last bundle's own rendition when a preset gets split
    /// into several self-finalizing dispatch bundles; a completed scan always
    /// sees every rendition every bundle ever published). A no-op for a
    /// non-HLS item (<paramref name="video"/> empty — GetVideoHashList only
    /// ever populates from <c>video_*/</c> dirs, which only exist for HLS
    /// output). Best-effort: a rebuild failure must never fail the scan.
    /// </summary>
    private async Task RebuildHlsMasterFromDiskAsync(
        IStorage storage,
        string hostFolder,
        string fileName,
        List<IVideo> video
    )
    {
        if (video.Count == 0)
            return;

        string masterTitle = fileName.TrimStart(trimChar: '/');
        if (masterTitle.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase))
            masterTitle = masterTitle[..^".m3u8".Length];

        // The rebuild ffprobes every video rendition over the network (a remote
        // backend copies each probe segment off first) and rewrites the master —
        // encode-finalize cost re-paid on every catalog scan. When the on-disk
        // master already advertises every rendition present on disk, that whole
        // pass reproduces the same master byte-for-byte, so skip it. Only a
        // genuinely incomplete master — the multi-bundle ladder split this exists
        // for, where a later bundle publishes a rung the current master never
        // listed — falls through to the full reconstruction.
        if (MasterCoversDiskOutput(storage: storage, hostFolder: hostFolder, masterTitle: masterTitle, video: video))
            return;

        // Only logged when the rebuild actually runs (a missing or incomplete
        // master), so it stays quiet on the common already-complete rescan.
        Logger.App(
            message: $"[MasterRebuild] {masterTitle}: rebuilding (master missing or incomplete)",
            level: LogEventLevel.Information
        );

        try
        {
            HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: mediaAnalyzer);
            OutputPlan plan = await reconstructor.ReconstructAsync(
                storage: storage,
                outputDirectory: hostFolder,
                ct: CancellationToken.None
            );

            if (plan.VideoOutputs.Length == 0)
                return;

            HlsOutputStrategy hlsOutputStrategy = new(storage: storage);
            await hlsOutputStrategy.FinalizeAsync(
                outputDirectory: hostFolder,
                plan: plan,
                mediaTitle: masterTitle,
                ct: CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Logger.App(
                message: $"[RebuildHlsMasterFromDisk] {masterTitle}: master rebuild failed — {ex.Message}",
                level: LogEventLevel.Warning
            );
        }
    }

    /// <summary>
    /// True when the on-disk master <c>{masterTitle}.m3u8</c> already advertises
    /// every rendition present under <paramref name="hostFolder"/> — every
    /// <paramref name="video"/> and <c>audio_*/</c> playlist, and every
    /// <c>subtitles/{lang}/{variant}.{m3u8|ass|srt}</c> language-subdir track the
    /// master references (never the raw root sidecar the OCR pass drops; that
    /// surfaces through <see cref="GetSubtitles"/> on the VideoFile, not the
    /// master). In that state a rebuild reproduces the same master byte-for-byte,
    /// so the caller skips the per-rendition ffprobe + master rewrite. A missing
    /// master or any on-disk rendition the master does not list returns false so
    /// the rebuild runs — the multi-bundle self-heal, where a later bundle
    /// publishes a rung the current master never advertised. Only checks disk ⊆
    /// master: a rendition deleted after finalize leaves a stale over-reference the
    /// next real finalize prunes — not a scan concern.
    /// </summary>
    internal static bool MasterCoversDiskOutput(
        IStorage storage,
        string hostFolder,
        string masterTitle,
        List<IVideo> video
    )
    {
        string masterPath = storage.CombinePath(parent: hostFolder, child: masterTitle + ".m3u8");
        if (!storage.Exists(path: masterPath))
            return false;

        string master;
        try
        {
            master = storage
                .ReadAllTextAsync(path: masterPath, ct: CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return false;
        }

        foreach (IVideo entry in video)
        {
            string renditionDir = entry.FileName.TrimStart(trimChar: '/').Split(separator: '/')[0];
            if (!master.Contains(value: renditionDir + "/", comparisonType: StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (
            StorageEntry dir in storage
                .List(path: hostFolder, pattern: "audio_*", recursive: false)
                .Where(predicate: entry => entry.IsDirectory)
        )
        {
            string renditionDir = storage.GetName(path: dir.Path);
            if (!master.Contains(value: renditionDir + "/", comparisonType: StringComparison.OrdinalIgnoreCase))
                return false;
        }

        string subtitleFolder = storage.CombinePath(parent: hostFolder, child: "subtitles");
        if (!storage.Exists(path: subtitleFolder))
            return true;

        foreach (
            StorageEntry languageDir in storage
                .List(path: subtitleFolder, pattern: "*", recursive: false)
                .Where(predicate: entry => entry.IsDirectory)
        )
        {
            string language = storage.GetName(path: languageDir.Path);
            foreach (
                StorageEntry track in storage
                    .List(path: languageDir.Path, pattern: "*", recursive: false)
                    .Where(predicate: entry => !entry.IsDirectory)
            )
            {
                string trackName = storage.GetName(path: track.Path);
                bool isAdvertised =
                    trackName.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
                    || trackName.EndsWith(value: ".ass", comparisonType: StringComparison.OrdinalIgnoreCase)
                    || trackName.EndsWith(value: ".srt", comparisonType: StringComparison.OrdinalIgnoreCase);
                if (!isAdvertised)
                    continue;

                if (!master.Contains(value: $"{language}/{trackName}", comparisonType: StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }
}
