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
using System.Text.RegularExpressions;
using MovieFileLibrary;
using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FFProbe;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Storage;
using Microsoft.Extensions.Logging;
namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Lists media files in a directory and projects them to FileItem (slice 16.6).
/// Enumerate -> parse -> IMediaIdentificationService.IdentifyAsync -> project.
/// A null identification result still emits a FileItem (Parsed populated, Match
/// empty): identification is enrichment, never a filter.
/// </summary>
public class FileListService(
    IStorageDriver storageDriver,
    IMediaIdentificationService identification,
    IFilenameParserPipeline filenameParser,
    ILogger<FileListService> logger
) : IFileListService
{
    public FileInfo[] GetVideoFilesInDirectory(string directoryPath)
    {
        return storageDriver
            .EnumerateFileSystemEntries(directory: directoryPath, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .Where(predicate: p => !storageDriver.DirectoryExists(path: p))
            .Where(predicate: p =>
            {
                string ext = Path.GetExtension(path: p);
                return ext is ".mkv" or ".mp4" or ".avi" or ".webm" or ".flv";
            })
            .Select(selector: p => new FileInfo(fileName: p))
            .ToArray();
    }

    public FileInfo[] GetAudioFilesInDirectory(string directoryPath)
    {
        return storageDriver
            .EnumerateFileSystemEntries(directory: directoryPath, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .Where(predicate: p => !storageDriver.DirectoryExists(path: p))
            .Where(predicate: p =>
            {
                string ext = Path.GetExtension(path: p);
                return ext is ".mp3" or ".flac" or ".wav" or ".m4a";
            })
            .Select(selector: p => new FileInfo(fileName: p))
            .ToArray();
    }

    private static readonly string[] VideoExtensions = [".mkv", ".mp4", ".avi", ".webm", ".flv"];
    private static readonly string[] AudioExtensions = [".mp3", ".flac", ".wav", ".m4a"];

    public async Task<List<FileItem>> GetFilesInDirectory(string directoryPath, string libraryType)
    {
        DirectoryInfo directoryInfo = new(path: directoryPath);

        FileInfo[] videoFiles = GetVideoFilesInDirectory(directoryPath: directoryPath);

        FileInfo[] audioFiles = GetAudioFilesInDirectory(directoryPath: directoryPath);

        ConcurrentBag<FileItem> fileList = [];
        if (videoFiles.Length == 0 && audioFiles.Length == 0)
            return fileList.ToList();

        if (audioFiles.Length > 0 && videoFiles.Length == 0)
        {
            (int year, string albumName, _, _) =
                MusicPathParser.Parse(directoryPath: directoryPath, folderName: directoryInfo.Name);

            await Parallel.ForEachAsync(
                source: audioFiles,
                parallelOptions: SystemParallelism.Options,
                body: (file, _) =>
                {
                    fileList.Add(
                        item: new()
                        {
                            Size = file.Length,
                            Mode = (int)file.Attributes,
                            Name = Path.Combine(path1: directoryPath, path2: file.Name),
                            Parent = directoryPath,
                            Parsed = new(filePath: directoryPath)
                            {
                                Title =
                                    albumName + " - " + Path.GetFileNameWithoutExtension(path: file.Name),
                                Year = year.ToString(),
                                IsSeries = false,
                                IsSuccess = true,
                            },
                            Match = new() { Title = albumName },
                            Path = Path.Combine(path1: directoryPath, path2: file.FullName),
                        }
                    );
                    return ValueTask.CompletedTask;
                }
            );
        }
        else if (videoFiles.Length > 0)
        {
            // Process video files sequentially - each file may add shows/movies to the DB,
            // and parallel processing causes race conditions when the show isn't known yet
            // (multiple iterations see "not found" simultaneously, all try to add, some fail)
            foreach (FileInfo file in videoFiles)
            {
                try
                {
                    await ProcessVideoFileInfo(libraryType: libraryType, file: file, fileList: fileList);
                }
                catch (Exception e)
                {
                    logger.LogError(message: e.Message);
                }
            }
        }

        return fileList.OrderBy(keySelector: file => file.Name).ToList();
    }

    /// <summary>
    /// Driver-aware variant of <see cref="GetFilesInDirectory"/>. Uses
    /// <paramref name="storage"/> to enumerate entries so the path never
    /// touches the local filesystem — required for NFS / S3 / WebDAV sources.
    /// ffprobe receives a real local path via <see cref="IStorage.AcquireLocalPath"/>.
    /// </summary>
    public async Task<List<FileItem>> GetFilesInDirectory(
        string directoryPath,
        string libraryType,
        IStorage storage
    )
    {
        IReadOnlyList<StorageEntry> allEntries = storage.List(
            path: directoryPath,
            pattern: null,
            recursive: false
        );

        StorageEntry[] videoEntries = allEntries
            .Where(predicate: e =>
                !e.IsDirectory
                && VideoExtensions.Contains(
                    value: Path.GetExtension(path: e.Path),
                    comparer: StringComparer.OrdinalIgnoreCase
                )
            )
            .ToArray();

        StorageEntry[] audioEntries = allEntries
            .Where(predicate: e =>
                !e.IsDirectory
                && AudioExtensions.Contains(
                    value: Path.GetExtension(path: e.Path),
                    comparer: StringComparer.OrdinalIgnoreCase
                )
            )
            .ToArray();

        ConcurrentBag<FileItem> fileList = [];

        if (videoEntries.Length == 0 && audioEntries.Length == 0)
            return fileList.ToList();

        if (audioEntries.Length > 0 && videoEntries.Length == 0)
        {
            // Use the same audio pattern as the local path, but derive folder
            // name from the driver-relative directoryPath.
            string folderName = StoragePathHelpers.GetName(path: directoryPath);

            (int year, string albumName, _, _) =
                MusicPathParser.Parse(directoryPath: directoryPath, folderName: folderName);

            await Parallel.ForEachAsync(
                source: audioEntries,
                parallelOptions: SystemParallelism.Options,
                body: (entry, _) =>
                {
                    string name = StoragePathHelpers.GetName(path: entry.Path);
                    fileList.Add(
                        item: new()
                        {
                            Size = entry.SizeBytes,
                            Mode = 0,
                            Name = entry.Path,
                            Parent = directoryPath,
                            Parsed = new(filePath: directoryPath)
                            {
                                Title =
                                    albumName
                                    + " - "
                                    + StoragePathHelpers.GetNameWithoutExtension(path: entry.Path),
                                Year = year.ToString(),
                                IsSeries = false,
                                IsSuccess = true,
                            },
                            Match = new() { Title = albumName },
                            Path = entry.Path,
                        }
                    );
                    return ValueTask.CompletedTask;
                }
            );
        }
        else if (videoEntries.Length > 0)
        {
            foreach (StorageEntry entry in videoEntries)
            {
                try
                {
                    await ProcessVideoStorageEntry(libraryType: libraryType, entry: entry, storage: storage, fileList: fileList);
                }
                catch (Exception e)
                {
                    logger.LogError(message: e.Message);
                }
            }
        }

        return fileList.OrderBy(keySelector: file => file.Name).ToList();
    }

    private async Task<bool> ProcessVideoStorageEntry(
        string libraryType,
        StorageEntry entry,
        IStorage storage,
        ConcurrentBag<FileItem> fileList
    )
    {
        string entryPath = entry.Path;
        string fileName = StoragePathHelpers.GetName(path: entryPath);
        string? directoryName = StoragePathHelpers.GetParent(path: entryPath);

        // Build a synthetic FileInfo-like object using storage metadata so the
        // parsing helpers stay unchanged. We do not touch raw System.IO here.
        string rawFileName = StoragePathHelpers.GetNameWithoutExtension(path: entryPath);

        int? overrideTmdbId = rawFileName.TryGetTmdbHint();

        string cleanedForYear = StringExtensions
            .RemoveBracketedString()
            .Replace(input: rawFileName, replacement: string.Empty);
        string? extractedYear = cleanedForYear.TryGetYear();

        string title = entryPath.Replace(oldValue: "v2", newValue: "");
        title = StringExtensions.RemoveBracketedString().Replace(input: title, replacement: string.Empty);

        // Filelist runs FOR EVERY FILE the user wants to triage; ffprobe over
        // a non-seekable stdin pipe scans to EOF on container formats whose
        // duration lives at end-of-file (mp4 mvhd, etc.) — 30 s/file on NFS.
        // The encode queue probes the full file later anyway. Skip ffprobe
        // here for remote drivers; the only reliable consumer of Streams in
        // the filelist response is the local-encode pre-flight which still
        // works end-to-end for LocalStorage.
        FfProbeData ffprobeData =
            storage.Driver is Storage.Drivers.Local.LocalStorageDriver
                ? await FfProbe.CreateAsync(file: entryPath)
                : new();

        MovieFile parsed = ParseVideoFileName(fileNameWithExt: fileName, directoryName: directoryName, title: title, libraryType: libraryType);

        parsed.Year = extractedYear ?? parsed.Year;
        if (parsed.Title == null)
            return true;

        parsed.Title = StringExtensions
            .RemoveParenthesizedString()
            .Replace(input: parsed.Title, replacement: string.Empty);

        bool seasonExplicit = parsed.Season.HasValue;

        // Dated daily episode (yyyy.mm.dd): the air date is the key, so discard any
        // stray number that would otherwise be misread as an episode. The resolver
        // maps the date to the episode that aired that day.
        DateOnly? airDate =
            libraryType is MediaTypes.TvMediaType or MediaTypes.AnimeMediaType
                ? DailyEpisodeParser.TryGetAirDate(name: rawFileName)
                : null;
        if (airDate.HasValue)
        {
            parsed.Season = null;
            parsed.Episode = null;
        }

        // Plex-style layout: when the file omits the season but lives in a
        // "Season N" folder, take the season from the folder instead of
        // blindly defaulting to 1. seasonExplicit is intentionally left as the
        // filename-derived value so the absolute-index fallback stays available.
        int? folderSeason = directoryName.TryGetFolderSeason();

        if (!airDate.HasValue && parsed is { Episode: not null, Season: null })
            parsed.Season = folderSeason ?? 1;

        if (!airDate.HasValue && !parsed.Season.HasValue && !parsed.Episode.HasValue)
        {
            Regex regex = StringExtensions.MatchNumbers();
            Match numberMatch = regex.Match(input: parsed.Title);
            if (numberMatch.Success)
            {
                parsed.Season = folderSeason ?? 1;
                parsed.Episode = int.Parse(s: numberMatch.Value);
                parsed.Title = regex.Split(input: parsed.Title).FirstOrDefault();
            }
        }

        (MovieOrEpisode episodeMatch, string? imdbId)? result = await identification.IdentifyAsync(
            parsed: parsed,
            libraryType: libraryType,
            duration: ffprobeData.Format.Duration,
            overrideTmdbId: overrideTmdbId,
            seasonExplicit: seasonExplicit,
            airDate: airDate
        );

        MovieOrEpisode match = new();
        if (result != null)
        {
            parsed.ImdbId = result.Value.imdbId;
            match = result.Value.episodeMatch;
        }

        // For driver-relative paths the "parent" is the directory that contains
        // the season folder, i.e. one level above directoryName.
        string? parentPath = string.IsNullOrEmpty(value: directoryName)
            ? "/"
            : StoragePathHelpers.GetParent(path: directoryName) ?? "/";

        ApplyEpisodeCardLabel(parsed: parsed, match: match);

        fileList.Add(
            item: new()
            {
                Size = entry.SizeBytes,
                Mode = 0,
                Name = StoragePathHelpers.GetNameWithoutExtension(path: fileName),
                Parent = parentPath,
                Parsed = parsed,
                Match = match,
                Path = entryPath,
                Streams = new()
                {
                    Video = ffprobeData.VideoStreams.Select(selector: video => new Video
                    {
                        Index = video.Index,
                        Width = video.Width,
                        Height = video.Height,
                    }),
                    Audio = ffprobeData.AudioStreams.Select(selector: stream => new Audio
                    {
                        Index = stream.Index,
                        Language = stream.Language,
                    }),
                    Subtitle = ffprobeData.SubtitleStreams.Select(selector: stream => new Subtitle
                    {
                        Index = stream.Index,
                        Language = stream.Language ?? "und",
                    }),
                },
            }
        );

        return result != null;
    }

    /// <summary>
    /// Replaces parsed.Title with the canonical TMDB English show name so the
    /// dashboard's '<parsed.title> SxxExx - <match.title>' template renders a
    /// label consistent with Stoney's file-naming convention even when the
    /// source filename uses a transliterated / fan-sub title.
    /// Movies and unmatched items keep their filename-derived parsed.Title.
    /// </summary>
    private static void ApplyEpisodeCardLabel(MovieFile parsed, MovieOrEpisode match)
    {
        if (string.IsNullOrWhiteSpace(value: match.ShowName))
            return;

        parsed.Title = match.ShowName;
    }

    private async Task<bool> ProcessVideoFileInfo(
        string libraryType,
        FileInfo file,
        ConcurrentBag<FileItem> fileList
    )
    {
        string rawFileName = Path.GetFileNameWithoutExtension(path: file.Name);

        // Check for a [tmdb-1234] hint baked into the filename, e.g. "[tmdb-553604]Spring (2019).mkv"
        int? overrideTmdbId = rawFileName.TryGetTmdbHint();

        string cleanedForYear = StringExtensions
            .RemoveBracketedString()
            .Replace(input: rawFileName, replacement: string.Empty);
        string? extractedYear = cleanedForYear.TryGetYear();

        string title = file.FullName.Replace(oldValue: "v2", newValue: "");
        title = StringExtensions.RemoveBracketedString().Replace(input: title, replacement: string.Empty);

        FfProbeData ffprobeData = await FfProbe.CreateAsync(file: file.FullName);
        MovieFile parsed = ParseVideoFileName(file: file, title: title, libraryType: libraryType);

        parsed.Year = extractedYear ?? parsed.Year;
        if (parsed.Title == null)
            return true;

        parsed.Title = StringExtensions
            .RemoveParenthesizedString()
            .Replace(input: parsed.Title, replacement: string.Empty);

        // Track whether the season came from the filename or was defaulted to 1.
        // This controls whether the absolute-index fallback is allowed in ResolveShowEpisodeAsync.
        bool seasonExplicit = parsed.Season.HasValue;

        // Dated daily episode (yyyy.mm.dd): the air date is the key, so discard any
        // stray number that would otherwise be misread as an episode. The resolver
        // maps the date to the episode that aired that day.
        DateOnly? airDate =
            libraryType is MediaTypes.TvMediaType or MediaTypes.AnimeMediaType
                ? DailyEpisodeParser.TryGetAirDate(name: rawFileName)
                : null;
        if (airDate.HasValue)
        {
            parsed.Season = null;
            parsed.Episode = null;
        }

        // Plex-style layout: when the file omits the season but lives in a
        // "Season N" folder, take the season from the folder instead of
        // blindly defaulting to 1. seasonExplicit is intentionally left as the
        // filename-derived value so the absolute-index fallback stays available.
        int? folderSeason = file.DirectoryName.TryGetFolderSeason();

        if (!airDate.HasValue && parsed is { Episode: not null, Season: null })
            parsed.Season = folderSeason ?? 1;

        if (!airDate.HasValue && !parsed.Season.HasValue && !parsed.Episode.HasValue)
        {
            Regex regex = StringExtensions.MatchNumbers();
            Match numberMatch = regex.Match(input: parsed.Title);
            if (numberMatch.Success)
            {
                parsed.Season = folderSeason ?? 1;
                parsed.Episode = int.Parse(s: numberMatch.Value);
                parsed.Title = regex.Split(input: parsed.Title).FirstOrDefault();
            }
        }

        (MovieOrEpisode episodeMatch, string? imdbId)? result = await identification.IdentifyAsync(
            parsed: parsed,
            libraryType: libraryType,
            duration: ffprobeData.Format.Duration,
            overrideTmdbId: overrideTmdbId,
            seasonExplicit: seasonExplicit,
            airDate: airDate
        );

        MovieOrEpisode match = new();
        if (result != null)
        {
            parsed.ImdbId = result.Value.imdbId;
            match = result.Value.episodeMatch;
        }
        fileList.Add(item: BuildFileItem(file: file, parsed: parsed, match: match, ffprobeData: ffprobeData));

        return result != null;
    }

    private MovieFile ParseVideoFileName(FileInfo file, string title, string libraryType) =>
        ParseVideoFileName(fileNameWithExt: file.Name, directoryName: file.DirectoryName, title: title, libraryType: libraryType);

    private MovieFile ParseVideoFileName(
        string fileNameWithExt,
        string? directoryName,
        string title,
        string libraryType
    )
    {
        string cleanedFileName = StringExtensions
            .RemoveBracketedString()
            .Replace(input: Path.GetFileNameWithoutExtension(path: fileNameWithExt), replacement: string.Empty)
            .Trim();

        return filenameParser.Parse(
            context: new()
            {
                FileNameWithExtension = fileNameWithExt,
                DirectoryName = directoryName,
                Title = title,
                CleanedFileName = cleanedFileName,
                FolderTitle = ExtractTitleFromFolder(directoryName: directoryName),
                LibraryType = libraryType,
            }
        );
    }

    private static string ExtractTitleFromFolder(string? directoryName)
    {
        string? folderName = Path.GetFileName(path: directoryName);
        if (string.IsNullOrWhiteSpace(value: folderName))
            return "";

        string cleaned = StringExtensions.RemoveBracketedString().Replace(input: folderName, replacement: string.Empty);
        cleaned = StringExtensions.RemoveParenthesizedString().Replace(input: cleaned, replacement: string.Empty);

        Match seasonTag = StringExtensions.MatchSeasonTag().Match(input: cleaned);
        if (seasonTag is { Success: true, Index: > 0 })
            cleaned = cleaned[..seasonTag.Index];

        string folderTitle = cleaned
            .Replace(oldChar: '.', newChar: ' ')
            .Replace(oldChar: '_', newChar: ' ')
            .TrimEnd(trimChars: ['-', '.', '_', ' '])
            .Trim();

        // Strip trailing year from folder-derived title (year is captured separately by TryGetYear)
        Match yearInFolder = StringExtensions.MatchYearRegex().Match(input: folderTitle);
        if (yearInFolder.Success)
            folderTitle = folderTitle[..yearInFolder.Index].TrimEnd(trimChars: ['-', '.', '_', ' ']);

        return folderTitle;
    }

    private static FileItem BuildFileItem(
        FileInfo file,
        MovieFile parsed,
        MovieOrEpisode match,
        FfProbeData ffprobeData
    )
    {
        string? parentPath = string.IsNullOrEmpty(value: file.DirectoryName)
            ? "/"
            : Path.GetDirectoryName(path: Path.Combine(path1: file.DirectoryName, path2: ".."));

        ApplyEpisodeCardLabel(parsed: parsed, match: match);

        return new()
        {
            Size = file.Length,
            Mode = (int)file.Attributes,
            Name = Path.GetFileNameWithoutExtension(path: file.Name),
            Parent = parentPath,
            Parsed = parsed,
            Match = match,
            Path = file.FullName,
            Streams = new()
            {
                Video = ffprobeData.VideoStreams.Select(selector: video => new Video
                {
                    Index = video.Index,
                    Width = video.Width,
                    Height = video.Height,
                }),
                Audio = ffprobeData.AudioStreams.Select(selector: stream => new Audio
                {
                    Index = stream.Index,
                    Language = stream.Language,
                }),
                Subtitle = ffprobeData.SubtitleStreams.Select(selector: stream => new Subtitle
                {
                    Index = stream.Index,
                    Language = stream.Language ?? "und",
                }),
            },
        };
    }
}
