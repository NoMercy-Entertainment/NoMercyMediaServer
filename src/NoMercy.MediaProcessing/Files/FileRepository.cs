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
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FFProbe;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;

public class FileRepository(MediaContext context, IStorageDriver storageDriver) : IFileRepository
{
    public IStorageDriver StorageDriver => storageDriver;

    public Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return context.Database.BeginTransactionAsync();
    }

    public async Task StoreVideoFile(VideoFile videoFile)
    {
        VideoFile? existing = await context.VideoFiles.FirstOrDefaultAsync(predicate: v =>
            v.Filename == videoFile.Filename && v.HostFolder == videoFile.HostFolder
        );

        if (existing is null)
        {
            context.VideoFiles.Add(entity: videoFile);
            await context.SaveChangesAsync();
            Logger.App(
                message: $"[StoreVideoFile] inserted {videoFile.Filename}",
                level: LogEventLevel.Information
            );
            return;
        }

        existing.EpisodeId = videoFile.EpisodeId;
        existing.LastEpisodeNumber = videoFile.LastEpisodeNumber;
        existing.MovieId = videoFile.MovieId;
        existing.Folder = videoFile.Folder;
        existing.HostFolder = videoFile.HostFolder;
        existing.Filename = videoFile.Filename;
        existing.Share = videoFile.Share;
        existing.Duration = videoFile.Duration;
        existing.Chapters = videoFile.Chapters;
        existing.Languages = videoFile.Languages;
        existing.Quality = videoFile.Quality;
        existing.Subtitles = videoFile.Subtitles;
        existing._tracks = videoFile._tracks;
        existing.MetadataId = videoFile.MetadataId;

        await context.SaveChangesAsync();
        Logger.App(message: $"[StoreVideoFile] updated {videoFile.Filename}", level: LogEventLevel.Information);
    }

    public async Task<Ulid> StoreMetadata(Metadata metadata)
    {
        Metadata? existing = await context.Metadata.FirstOrDefaultAsync(predicate: m =>
            m.Filename == metadata.Filename && m.HostFolder == metadata.HostFolder
        );

        if (existing is null)
        {
            context.Metadata.Add(entity: metadata);
            await context.SaveChangesAsync();
            Logger.App(
                message: $"[StoreMetadata] inserted {metadata.Filename} (id={metadata.Id})",
                level: LogEventLevel.Information
            );
            return metadata.Id;
        }

        existing.AudioTrackId = metadata.AudioTrackId;
        existing.Duration = metadata.Duration;
        existing.Filename = metadata.Filename;
        existing.Folder = metadata.Folder;
        existing.FolderSize = metadata.FolderSize;
        existing.HostFolder = metadata.HostFolder;
        existing.Type = metadata.Type;
        existing._audio = metadata._audio;
        existing._chapters = metadata._chapters;
        existing._chapters_file = metadata._chapters_file;
        existing._fonts = metadata._fonts;
        existing._fonts_file = metadata._fonts_file;
        existing._previews = metadata._previews;
        existing._subtitles = metadata._subtitles;
        existing._video = metadata._video;

        await context.SaveChangesAsync();
        Logger.App(
            message: $"[StoreMetadata] updated {metadata.Filename} (id={existing.Id})",
            level: LogEventLevel.Information
        );
        return existing.Id;
    }

    public async Task<Episode?> GetEpisode(int? showId, MediaFile item)
    {
        if (item.Parsed == null)
            return null;

        return await context
            .Episodes.Where(predicate: e => e.TvId == showId)
            .Where(predicate: e => e.SeasonNumber == item.Parsed!.Season)
            .Where(predicate: e => e.EpisodeNumber == item.Parsed!.Episode)
            .FirstOrDefaultAsync();
    }

    public async Task<(Movie? movie, Tv? show, string type)> MediaType(int id, Library library)
    {
        Movie? movie = null;
        Tv? show = null;
        string type = "";

        switch (library.Type)
        {
            case MediaTypes.MovieMediaType:
                movie = await context
                    .Movies.IgnoreQueryFilters()
                    .Where(predicate: m => m.Id == id)
                    .FirstOrDefaultAsync();
                type = library.Type;
                break;
            case MediaTypes.TvMediaType:
            case MediaTypes.AnimeMediaType:
                show = await context.Tvs.Where(predicate: t => t.Id == id).FirstOrDefaultAsync();

                if (show == null)
                {
                    Episode? episode = await context
                        .Episodes.Where(predicate: e => e.Id == id)
                        .FirstOrDefaultAsync();

                    if (episode != null)
                    {
                        show = await context
                            .Tvs.Where(predicate: t => t.Id == episode.TvId)
                            .FirstOrDefaultAsync();
                    }
                }

                type = library.Type;
                break;
        }

        return (movie, show, type);
    }

    /// <summary>
    /// Extracts the underlying <see cref="IStorageDriver"/> from an <see cref="IStorage"/>
    /// instance. Used by callers (e.g. <see cref="MediaScan"/>) that accept the lower-level
    /// interface directly.
    /// </summary>
    public static IStorageDriver StorageDriverFromStorage(IStorage storage) => storage.Driver;

    // Tracks recent CoverArt search queries to avoid duplicate lookups within a scan.
    private static readonly List<string> PrevSearchQueries = [];

    public static async Task<List<FileItem>> GetMusicBrainzReleasesInDirectory(
        string folder,
        IStorageDriver storageDriver,
        IAudioFingerprinter audioFingerprinter
    )
    {
        PrevSearchQueries.Clear();

        MediaScan mediaScan = new(driver: storageDriver);
        ConcurrentBag<MediaFolderExtend> mediaFolders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType(mediaType: "music")
            .Process(rootFolder: folder, depth: 2);

        if (mediaFolders.Count == 0)
            return [];

        ConcurrentBag<MediaFile> mediaFiles = mediaFolders
            .SelectMany(selector: m => m.Files ?? [])
            .ToConcurrentBag();

        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        using MusicBrainzRecordingClient musicBrainzRecordingClient = new();
        List<Guid> lookupReleaseIds = [];

        (List<MusicBrainzReleaseAppends> releases, string year) =
            await SearchForReleasesFromMediaFiles(
                mediaFiles: mediaFiles,
                musicBrainzReleaseClient: musicBrainzReleaseClient,
                lookupReleaseIds: lookupReleaseIds,
                musicBrainzRecordingClient: musicBrainzRecordingClient,
                audioFingerprinter: audioFingerprinter
            );

        releases = await FetchReleaseAppends(lookupReleaseIds: lookupReleaseIds, musicBrainzReleaseClient: musicBrainzReleaseClient, releases: releases);
        List<FileItem> files = await GenerateResponse(folder: folder, releases: releases, mediaFiles: mediaFiles, year: year);
        return files;
    }

    private static async Task<(
        List<MusicBrainzReleaseAppends> releases,
        string year
    )> SearchForReleasesFromMediaFiles(
        ConcurrentBag<MediaFile> mediaFiles,
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        List<Guid> lookupReleaseIds,
        MusicBrainzRecordingClient musicBrainzRecordingClient,
        IAudioFingerprinter audioFingerprinter
    )
    {
        string prevMusicBrainzReleaseId = string.Empty;
        string year = "0";
        List<MusicBrainzReleaseAppends> releases = [];
        object lockObject = new();

        await Parallel.ForEachAsync(
            source: mediaFiles,
            parallelOptions: SystemParallelism.Options,
            body: async (mediaFile, _) =>
            {
                AudioTagModel audioTagModel = await AudioTagModel.Create(fileItem: mediaFile);

                if (audioTagModel.Tags == null)
                    return;
                if (!string.IsNullOrEmpty(value: audioTagModel.Tags.MusicBrainzReleaseId))
                {
                    (prevMusicBrainzReleaseId, year) = await FromMusicBrainzRelease(
                        musicBrainzReleaseClient: musicBrainzReleaseClient,
                        audioTagModel: audioTagModel,
                        lockObject: lockObject,
                        releases: releases,
                        prevMusicBrainzReleaseId: prevMusicBrainzReleaseId,
                        year: year
                    );
                }
                else
                {
                    prevMusicBrainzReleaseId =
                        await FromFingerprint(
                            musicBrainzReleaseClient: musicBrainzReleaseClient,
                            mediaFile: mediaFile,
                            lockObject: lockObject,
                            releases: releases,
                            audioFingerprinter: audioFingerprinter
                        ) ?? prevMusicBrainzReleaseId;
                }
            }
        );
        releases = releases.Where(predicate: x => x.Id != Guid.Empty).DistinctBy(keySelector: x => x.Id).ToList();
        return (releases, year);
    }

    private static async Task<string?> FromFingerprint(
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        MediaFile mediaFile,
        object lockObject,
        List<MusicBrainzReleaseAppends> releases,
        IAudioFingerprinter audioFingerprinter
    )
    {
        string prevMusicBrainzReleaseId;
        AcoustIdFingerprintClient acoustIdFingerprintClient = new(fingerprinter: audioFingerprinter);
        AcoustIdFingerprint? acoustIds = await acoustIdFingerprintClient.Lookup(file: mediaFile.Path);
        if (acoustIds == null)
            return null;
        foreach (AcoustIdFingerprintResult fingerPrint in acoustIds?.Results ?? [])
        {
            foreach (AcoustIdFingerprintRecording? recording in fingerPrint.Recordings ?? [])
            {
                if (recording?.Releases is null)
                    continue;
                foreach (
                    AcoustIdFingerprintReleaseGroups acoustIdFingerprintReleaseGroups in recording.Releases
                )
                {
                    MusicBrainzReleaseAppends? release =
                        await musicBrainzReleaseClient.WithAllAppends(
                            id: acoustIdFingerprintReleaseGroups.Id
                        );

                    if (release == null || release.Id == Guid.Empty)
                        return null;
                    prevMusicBrainzReleaseId = release.Id.ToString();
                    lock (lockObject)
                    {
                        releases.Add(item: release);
                    }
                }
            }
        }

        return null;
    }

    private static async Task<(
        string prevMusicBrainzReleaseId,
        string year
    )> FromMusicBrainzRelease(
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        AudioTagModel audioTagModel,
        object lockObject,
        List<MusicBrainzReleaseAppends> releases,
        string prevMusicBrainzReleaseId,
        string year
    )
    {
        if (prevMusicBrainzReleaseId == audioTagModel.Tags?.MusicBrainzReleaseId)
        {
            if (year == "0")
                year = audioTagModel.Tags?.Year.ToString() ?? "0";
            return (prevMusicBrainzReleaseId, year);
        }

        Guid musicBrainzReleaseId = Guid.Parse(
            input: (audioTagModel.Tags?.MusicBrainzReleaseId).OrEmpty()
        );
        if (musicBrainzReleaseId == Guid.Empty)
            return (prevMusicBrainzReleaseId, year);
        MusicBrainzReleaseAppends? release = await musicBrainzReleaseClient.WithAllAppends(
            id: musicBrainzReleaseId
        );

        if (release == null || release.Id == Guid.Empty)
            return (prevMusicBrainzReleaseId, year);
        prevMusicBrainzReleaseId = release.Id.ToString();
        lock (lockObject)
        {
            releases.Add(item: release);
        }

        return (prevMusicBrainzReleaseId, year);
    }

    private static async Task<List<FileItem>> GenerateResponse(
        string folder,
        List<MusicBrainzReleaseAppends> releases,
        ConcurrentBag<MediaFile> mediaFiles,
        string year
    )
    {
        if (releases.Count == 0)
            return [];

        List<FileItem> files = [];

        MusicBrainzReleaseAppends? bestResult = await GetBestMatchedRelease(mediaFiles: mediaFiles, matchedReleases: releases);
        if (bestResult != null)
        {
            Logger.MusicBrainz(
                message: $"Best match: {bestResult.Title} - {bestResult.Id}",
                level: LogEventLevel.Verbose
            );

            Uri? coverPaletteUrl = await CoverArtImageManagerManager.GetCoverUrl(
                id: bestResult.Id,
                priority: true
            );

            files.Add(
                item: new()
                {
                    Size = mediaFiles.Sum(selector: x => x.Size),
                    Mode = 0,
                    Name = bestResult.Title,
                    Parent = folder,
                    Parsed = new(filePath: folder)
                    {
                        Title = bestResult.Title,
                        Year = bestResult.DateTime?.Year.ToString() ?? year,
                        IsSeries = false,
                        IsSuccess = true,
                    },
                    Match = new()
                    {
                        Id = bestResult.Id,
                        Title = bestResult.Title,
                        Still = coverPaletteUrl?.ToString(),
                    },
                    Path = folder,
                    Tracks = bestResult.Media.Sum(selector: m => m.TrackCount),
                    Streams = new()
                    {
                        Audio =
                        [
                            new()
                            {
                                Index = 0,
                                Language =
                                    $"Best Match {string.Join(separator: ", ", values: bestResult.Media.Select<MusicBrainzMedia, string>(selector: m => m.Format))}",
                            },
                        ],
                    },
                }
            );
        }

        await Parallel.ForEachAsync(
            source: releases,
            parallelOptions: SystemParallelism.Options,
            body: async (release, _) =>
            {
                if (files.Any(predicate: x => x.Match.Id == release.Id))
                    return;

                Uri? coverPaletteUrl = await CoverArtImageManagerManager.GetCoverUrl(
                    id: release.Id,
                    priority: true
                );

                files.Add(
                    item: new()
                    {
                        Size = mediaFiles.Sum(selector: x => x.Size),
                        Mode = 0,
                        Name = release.Title,
                        Parent = folder,
                        Parsed = new(filePath: folder)
                        {
                            Title = release.Title,
                            Year = release.DateTime?.Year.ToString() ?? year,
                            IsSeries = false,
                            IsSuccess = true,
                        },
                        Match = new()
                        {
                            Id = release.Id,
                            Title = release.Title,
                            Still = coverPaletteUrl?.ToString(),
                        },
                        Path = folder,
                        Tracks = release.Media.Sum(selector: m => m.TrackCount),
                        Streams = new()
                        {
                            Audio =
                            [
                                new()
                                {
                                    Index = 0,
                                    Language =
                                        $"Formats: {string.Join(separator: ", ", values: release.Media.Select(selector: m => m.Format))}",
                                },
                            ],
                        },
                    }
                );
            }
        );

        return files;
    }

    private static async Task<List<MusicBrainzReleaseAppends>> FetchReleaseAppends(
        List<Guid> lookupReleaseIds,
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        List<MusicBrainzReleaseAppends> releases
    )
    {
        object lockObject = new();
        lookupReleaseIds = lookupReleaseIds.DistinctBy(keySelector: x => x).ToList();
        await Parallel.ForEachAsync(
            source: lookupReleaseIds,
            parallelOptions: SystemParallelism.Options,
            body: async (releaseId, _) =>
            {
                MusicBrainzReleaseAppends? musicBrainzRelease =
                    await musicBrainzReleaseClient.WithAllAppends(id: releaseId, priority: true);
                if (musicBrainzRelease == null || releases.Any(predicate: r => r.Id == musicBrainzRelease.Id))
                    return;
                lock (lockObject)
                {
                    releases.Add(item: musicBrainzRelease);
                }
            }
        );

        return releases.Where(predicate: x => x.Id != Guid.Empty).DistinctBy(keySelector: x => x.Id).ToList();
    }

    private static async Task<MusicBrainzReleaseAppends?> GetBestMatchedRelease(
        ConcurrentBag<MediaFile> mediaFiles,
        List<MusicBrainzReleaseAppends> matchedReleases
    )
    {
        MusicBrainzReleaseAppends? bestRelease = null;
        int highestScore = 0;
        object lockObject = new();

        await Parallel.ForEachAsync(
            source: matchedReleases,
            parallelOptions: SystemParallelism.Options,
            body: async (release, cancellationToken) =>
            {
                int score = await CalculateMatchScoreAsync(release: release, localFiles: mediaFiles);
                lock (lockObject)
                {
                    if (score < highestScore)
                        return;
                    highestScore = score;
                    if (highestScore == mediaFiles.Count)
                        bestRelease = release;
                }
            }
        );

        return bestRelease;
    }

    private static async Task<int> CalculateMatchScoreAsync(
        MusicBrainzReleaseAppends release,
        ConcurrentBag<MediaFile> localFiles
    )
    {
        int score = 0;

        if (release.Media.Length == 0)
            return 0;

        await Parallel.ForEachAsync(
            source: release.Media,
            parallelOptions: SystemParallelism.Options,
            body: async (media, cancellationToken) =>
            {
                if (media.Tracks.Length == 0 || media.TrackCount == 0)
                    return;

                await Parallel.ForEachAsync(
                    source: localFiles,
                    parallelOptions: SystemParallelism.Options,
                    body: async (file, ct) =>
                    {
                        try
                        {
                            file.TagFile ??= TagFile.Create(path: file.Path);
                            file.FFprobe ??= await FfProbe.CreateAsync(file: file.Path, ct: ct);

                            int trackIndex = localFiles.ToList().IndexOf(item: file);
                            bool isMatch = media.Tracks.Any(predicate: track =>
                            {
                                bool nameMatch = CompareTrackName(mediaFile: file, track: track);
                                bool numberMatch = CompareTrackNumber(mediaFile: file, track: track, trackIndex: trackIndex);
                                bool durationMatch = CompareTrackDuration(mediaFile: file, track: track);
                                return nameMatch && numberMatch && durationMatch;
                            });

                            if (!isMatch)
                                return;
                            Interlocked.Increment(location: ref score);
                        }
                        catch (Exception ex)
                        {
                            Logger.MusicBrainz(
                                message: $"Error processing file {file.Path}: {ex.Message}",
                                level: LogEventLevel.Verbose
                            );
                        }
                    }
                );
            }
        );

        return score;
    }

    private static bool CompareTrackDuration(MediaFile mediaFile, MusicBrainzTrack track)
    {
        double duration = track.Duration;
        double tagDuration = mediaFile.TagFile?.Properties?.Duration.TotalSeconds ?? 0;
        double fileDuration = mediaFile.FFprobe?.Duration.TotalSeconds ?? 0;

        if (duration == 0 && fileDuration == 0 && tagDuration == 0)
            return false;

        return Math.Abs(value: duration - fileDuration).ToInt() < 3
            || Math.Abs(value: duration - tagDuration).ToInt() < 3;
    }

    private static bool CompareTrackNumber(
        MediaFile mediaFile,
        MusicBrainzTrack track,
        int trackIndex
    )
    {
        int trackNumber = track.Position;
        long tagTrackNumber = mediaFile.TagFile?.Tag?.Track ?? 0;
        int fileTrackNumber = mediaFile.Parsed?.TrackNumber ?? 0;

        if (trackNumber == 0 && fileTrackNumber == 0 && tagTrackNumber == 0)
            return false;

        return Math.Abs(value: trackNumber - fileTrackNumber) == 0
            || Math.Abs(value: trackNumber - trackIndex) == 0
            || (int)Math.Abs(value: trackNumber - tagTrackNumber) == 0;
    }

    private static bool CompareTrackName(MediaFile mediaFile, MusicBrainzTrack track)
    {
        string trackTitle = track.Title;
        string tagTitle =
            mediaFile.TagFile?.Tag?.Title ?? Path.GetFileNameWithoutExtension(path: mediaFile.Name);
        string fileTitle =
            mediaFile.Parsed?.Title ?? Path.GetFileNameWithoutExtension(path: mediaFile.Name);

        if (
            string.IsNullOrEmpty(value: trackTitle)
            && string.IsNullOrEmpty(value: fileTitle)
            && string.IsNullOrEmpty(value: tagTitle)
        )
            return false;

        return fileTitle.ContainsSanitized(value: trackTitle) || tagTitle.ContainsSanitized(value: trackTitle);
    }

    public async Task<int> DeleteVideoFilesByHostFolderAsync(string hostFolder)
    {
        return await context
            .VideoFiles.Where(predicate: vf => vf.HostFolder == hostFolder)
            .ExecuteDeleteAsync();
    }

    public async Task<int> DeleteMetadataByHostFolderAsync(string hostFolder)
    {
        return await context.Metadata.Where(predicate: m => m.HostFolder == hostFolder).ExecuteDeleteAsync();
    }

    public async Task<int> UpdateVideoFilePathsAsync(
        string oldHostFolder,
        string oldFilename,
        string newHostFolder,
        string newFilename
    )
    {
        string newFolder = "/" + StoragePathHelpers.GetName(path: newHostFolder);

        return await context
            .VideoFiles.Where(predicate: vf => vf.HostFolder == oldHostFolder && vf.Filename == oldFilename)
            .ExecuteUpdateAsync(setPropertyCalls: setters =>
                setters
                    .SetProperty(propertyExpression: vf => vf.HostFolder, valueExpression: newHostFolder)
                    .SetProperty(propertyExpression: vf => vf.Filename, valueExpression: newFilename)
                    .SetProperty(propertyExpression: vf => vf.Folder, valueExpression: newFolder)
            );
    }

    public async Task<int> UpdateVideoFileSubtitlesAsync(
        Ulid videoFileId,
        string subtitlesJson,
        CancellationToken ct = default
    )
    {
        return await context
            .VideoFiles.Where(predicate: vf => vf.Id == videoFileId)
            .ExecuteUpdateAsync(
                setPropertyCalls: setters => setters.SetProperty(propertyExpression: vf => vf.Subtitles, valueExpression: subtitlesJson),
                cancellationToken: ct
            );
    }

    public async Task DeleteVideoFilesAndMetadataByMovieIdAsync(int movieId)
    {
        List<Ulid> metadataIds = await context
            .VideoFiles.Where(predicate: vf => vf.MovieId == movieId && vf.MetadataId != null)
            .Select(selector: vf => vf.MetadataId!.Value)
            .ToListAsync();

        await context.VideoFiles.Where(predicate: vf => vf.MovieId == movieId).ExecuteDeleteAsync();

        if (metadataIds.Count > 0)
        {
            await context.Metadata.Where(predicate: m => metadataIds.Contains(m.Id)).ExecuteDeleteAsync();
        }
    }

    public async Task DeleteVideoFilesAndMetadataByTvIdAsync(int tvId)
    {
        List<int> episodeIds = await context
            .Episodes.Where(predicate: e => e.TvId == tvId)
            .Select(selector: e => e.Id)
            .ToListAsync();

        List<Ulid> metadataIds = await context
            .VideoFiles.Where(predicate: vf =>
                vf.EpisodeId != null
                && episodeIds.Contains(vf.EpisodeId.Value)
                && vf.MetadataId != null
            )
            .Select(selector: vf => vf.MetadataId!.Value)
            .ToListAsync();

        await context
            .VideoFiles.Where(predicate: vf => vf.EpisodeId != null && episodeIds.Contains(vf.EpisodeId.Value))
            .ExecuteDeleteAsync();

        if (metadataIds.Count > 0)
        {
            await context.Metadata.Where(predicate: m => metadataIds.Contains(m.Id)).ExecuteDeleteAsync();
        }
    }

    public List<DirectoryTree> GetDirectoryTree(string folder = "")
    {
        List<DirectoryTree> array = [];

        if (string.IsNullOrEmpty(value: folder) || folder == "/")
        {
            if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            {
                DriveInfo[] driveInfo = DriveInfo.GetDrives();
                return driveInfo
                    .Where(predicate: d => d.IsReady)
                    .Select(selector: d => new DirectoryTree(parent: d.RootDirectory.ToString(), path: ""))
                    .OrderBy(keySelector: file => file.Path)
                    .ToList();
            }

            folder = "/";
        }

        if (!storageDriver.DirectoryExists(path: folder))
            return array;

        IEnumerable<string> directories;
        try
        {
            directories = storageDriver
                .EnumerateFileSystemEntries(directory: folder, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
                .Where(predicate: e => storageDriver.DirectoryExists(path: e));
        }
        catch (IOException)
        {
            return array;
        }
        catch (UnauthorizedAccessException)
        {
            return array;
        }

        array = directories
            .Select(selector: d => new DirectoryTree(parent: folder, path: d))
            .OrderBy(keySelector: file => file.Path)
            .ToList();

        return array;
    }

    public Task<List<VideoFile>> SearchVideoFilesAsync(
        string? query,
        int limit,
        CancellationToken ct = default
    )
    {
        IQueryable<VideoFile> baseQuery = context
            .VideoFiles.AsNoTracking()
            .Include(navigationPropertyPath: file => file.Movie)
            .Include(navigationPropertyPath: file => file.Episode)
                .ThenInclude(navigationPropertyPath: episode => episode!.Tv);

        IQueryable<VideoFile> filtered = string.IsNullOrEmpty(value: query)
            ? baseQuery
            : baseQuery.Where(predicate: file =>
                EF.Functions.Like(file.Filename, $"%{query}%")
                || (file.Movie != null && EF.Functions.Like(file.Movie.Title, $"%{query}%"))
                || (file.Episode != null && EF.Functions.Like(file.Episode.Title!, $"%{query}%"))
                || (
                    file.Episode != null
                    && file.Episode.Tv != null
                    && EF.Functions.Like(file.Episode.Tv.Title, $"%{query}%")
                )
            );

        return filtered.OrderByDescending(keySelector: file => file.UpdatedAt).Take(count: limit).ToListAsync(cancellationToken: ct);
    }
}
