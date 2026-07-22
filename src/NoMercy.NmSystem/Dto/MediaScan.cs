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
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FFProbe;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Serilog.Events;

namespace NoMercy.NmSystem.Dto;

public class MediaScan : IDisposable, IAsyncDisposable
{
    private readonly MovieDetector _movieDetector = new();
    private readonly IStorageDriver _driver;

    private bool _fileListingEnabled;
    private bool _regexFilterEnabled = true;
    private string? Filter { get; set; }

    private readonly Regex _folderNameRegex = new(
        pattern: @"video_.*|audio_.*|subtitles|scans|cds.*|ost|album|music|original|fonts|thumbs|metadata|NCED|NCOP|\s\(\d\)\.|~",
        options: RegexOptions.IgnoreCase
    );

    private string[] _extensionFilter = [];

    public MediaScan(IStorageDriver driver)
    {
        _driver = driver;
    }

    public MediaScan EnableFileListing()
    {
        _fileListingEnabled = true;

        return this;
    }

    public MediaScan DisableRegexFilter()
    {
        _regexFilterEnabled = false;

        return this;
    }

    public MediaScan FilterByMediaType(string mediaType)
    {
        _extensionFilter = mediaType switch
        {
            "anime" or MediaTypes.TvMediaType or MediaTypes.MovieMediaType or "video" =>
            [
                ".mp4",
                ".avi",
                ".mkv",
                ".m3u8",
            ],
            "music" => [".mp3", ".flac", ".wav", ".m4a"],
            "subtitle" => [".srt", ".vtt", ".ass"],
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(mediaType), actualValue: mediaType, message: null),
        };

        return this;
    }

    public async Task<ConcurrentBag<MediaFolderExtend>> Process(string rootFolder, int depth = 0)
    {
        rootFolder = _driver.GetFullPath(path: rootFolder);
        return !_fileListingEnabled
            ? await Task.Run(function: () => ScanFoldersOnly(folderPath: rootFolder, depth: depth))
            : await ScanFolderAsync(folderPath: rootFolder, depth: depth);
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> ScanFolderAsync(
        string folderPath,
        int depth
    )
    {
        folderPath = _driver.GetFullPath(path: folderPath);

        ConcurrentBag<MediaFolderExtend> folders = [];

        if (depth < 0)
            return folders;

        if (!_driver.DirectoryExists(path: folderPath))
            return folders;

        ConcurrentBag<MediaFile> files = await FilesAsync(folderPath: folderPath);

        MovieFile movieFile1 = _movieDetector.GetInfo(filePath: folderPath);
        movieFile1.Year ??= folderPath.TryGetYear();

        folders.Add(
            item: new()
            {
                Name = Path.GetFileName(path: folderPath),
                Path = folderPath,
                Created = _driver.GetCreationTimeUtc(path: folderPath).ToLocalTime(),
                Modified = _driver.GetLastWriteTimeUtc(path: folderPath).ToLocalTime(),
                Accessed = _driver.GetLastAccessTimeUtc(path: folderPath).ToLocalTime(),
                Type = "folder",
                Parsed = new()
                {
                    Title = movieFile1.Title,
                    Year = movieFile1.Year,
                    FilePath = movieFile1.Path,
                },

                Files = files,
            }
        );

        try
        {
            IOrderedEnumerable<string>? directories = null;
            try
            {
                directories = _driver
                    .EnumerateFileSystemEntries(directory: folderPath, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
                    .Where(predicate: e => _driver.DirectoryExists(path: e))
                    .OrderBy(keySelector: f => f);
            }
            catch
            {
                // ignored
            }
            if (directories is null)
                return folders;

            await Parallel.ForEachAsync(
                source: directories,
                parallelOptions: SystemParallelism.Options,
                body: async (directory, cancellationToken) =>
                {
                    string folderName = Path.GetFileName(path: directory);

                    if ((_regexFilterEnabled && _folderNameRegex.IsMatch(input: folderName)) || depth == 0)
                    {
                        files.Add(
                            item: new()
                            {
                                Name = folderName,
                                Path = directory,
                                Created = _driver.GetCreationTimeUtc(path: directory).ToLocalTime(),
                                Modified = _driver.GetLastWriteTimeUtc(path: directory).ToLocalTime(),
                                Accessed = _driver.GetLastAccessTimeUtc(path: directory).ToLocalTime(),
                                Type = "folder",
                            }
                        );

                        return;
                    }

                    ConcurrentBag<MediaFile> files2 =
                        depth - 1 > 0 ? await FilesAsync(folderPath: directory) : [];

                    string cleanedFolderName = StringExtensions
                        .RemoveBracketedString()
                        .Replace(input: folderName, replacement: string.Empty)
                        .Trim();
                    string cleanedDirectory = Path.Combine(
                        path1: Path.GetDirectoryName(path: directory)!,
                        path2: cleanedFolderName
                    );
                    MovieFile movieFile = _movieDetector.GetInfo(filePath: cleanedDirectory);
                    movieFile.Year ??= directory.TryGetYear();
                    if (string.IsNullOrEmpty(value: movieFile.Title))
                        movieFile.Title = cleanedFolderName.Replace(oldChar: '.', newChar: ' ').Trim();

                    folders.Add(
                        item: new()
                        {
                            Name = folderName,
                            Path = directory,
                            Created = _driver.GetCreationTimeUtc(path: directory).ToLocalTime(),
                            Modified = _driver.GetLastWriteTimeUtc(path: directory).ToLocalTime(),
                            Accessed = _driver.GetLastAccessTimeUtc(path: directory).ToLocalTime(),
                            Type = "folder",
                            Parsed = new()
                            {
                                Title = movieFile.Title,
                                Year = movieFile.Year,
                                FilePath = movieFile.Path,
                            },

                            Files = files2.Count > 0 ? files2 : null,

                            SubFolders =
                                depth - 1 > 0 ? await ScanFolderAsync(folderPath: directory, depth: depth - 1) : [],
                        }
                    );
                }
            );

            ConcurrentBag<MediaFolderExtend> response = folders
                .Where(predicate: f => f.Name is not "")
                .OrderByDescending(keySelector: f => f.Name)
                .ToConcurrentBag();

            return response;
        }
        catch (Exception e)
        {
            Logger.App(message: e.Message, level: LogEventLevel.Fatal);
            throw;
        }
    }

    private ConcurrentBag<MediaFolderExtend> ScanFoldersOnly(string folderPath, int depth)
    {
        folderPath = _driver.GetFullPath(path: folderPath.ToUtf8());

        if (depth < 0)
            return [];

        try
        {
            ConcurrentBag<MediaFolderExtend> folders = [];

            IOrderedEnumerable<string> directories = _driver
                .EnumerateFileSystemEntries(directory: folderPath, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
                .Where(predicate: e => _driver.DirectoryExists(path: e))
                .OrderBy(keySelector: f => f);

            Parallel.ForEach(
                source: directories,
                parallelOptions: SystemParallelism.Options,
                body: (directory, _) =>
                {
                    string dir = _driver.GetFullPath(path: directory.ToUtf8());
                    Logger.App(message: $"Scanning {dir}");

                    string folderName = Path.GetFileName(path: dir);

                    if (_regexFilterEnabled && _folderNameRegex.IsMatch(input: folderName))
                    {
                        folders.Add(
                            item: new()
                            {
                                Name = folderName,
                                Path = dir,
                                Created = _driver.GetCreationTimeUtc(path: dir).ToLocalTime(),
                                Modified = _driver.GetLastWriteTimeUtc(path: dir).ToLocalTime(),
                                Accessed = _driver.GetLastAccessTimeUtc(path: dir).ToLocalTime(),
                                Type = "folder",
                            }
                        );

                        return;
                    }

                    string cleanedFolderName = StringExtensions
                        .RemoveBracketedString()
                        .Replace(input: folderName, replacement: string.Empty)
                        .Trim();
                    string cleanedDirectory = Path.Combine(
                        path1: Path.GetDirectoryName(path: directory)!,
                        path2: cleanedFolderName
                    );
                    MovieFile movieFile = _movieDetector.GetInfo(filePath: cleanedDirectory);
                    movieFile.Year ??= directory.TryGetYear();
                    if (string.IsNullOrEmpty(value: movieFile.Title))
                        movieFile.Title = cleanedFolderName.Replace(oldChar: '.', newChar: ' ').Trim();

                    folders.Add(
                        item: new()
                        {
                            Name = folderName,
                            Path = directory,
                            Created = _driver.GetCreationTimeUtc(path: directory).ToLocalTime(),
                            Modified = _driver.GetLastWriteTimeUtc(path: directory).ToLocalTime(),
                            Accessed = _driver.GetLastAccessTimeUtc(path: directory).ToLocalTime(),
                            Type = "folder",

                            Parsed = new()
                            {
                                Title = movieFile.Title,
                                Year = movieFile.Year,
                                FilePath = movieFile.Path,
                            },

                            SubFolders = depth - 1 > 0 ? ScanFoldersOnly(folderPath: directory, depth: depth - 1) : [],
                        }
                    );
                }
            );

            ConcurrentBag<MediaFolderExtend> response = folders
                .Where(predicate: f => f.Name is not "")
                .OrderByDescending(keySelector: f => f.Name)
                .ToConcurrentBag();

            return response;
        }
        catch (Exception e)
        {
            Logger.App(message: e.Message, level: LogEventLevel.Fatal);
            throw;
        }
    }

    private async Task<ConcurrentBag<MediaFile>> FilesAsync(string folderPath)
    {
        ConcurrentBag<MediaFile> files = [];
        try
        {
            IEnumerable<string> entries = _driver
                .EnumerateFileSystemEntries(directory: folderPath, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
                .Where(predicate: e => _driver.FileExists(path: e));

            await Parallel.ForEachAsync(
                source: entries,
                parallelOptions: SystemParallelism.Options,
                body: async (file, cancellationToken) =>
                {
                    file = _driver.GetFullPath(path: file.ToUtf8());

                    if (Filter is not null)
                        if (!file.Contains(value: Filter))
                            return;

                    string extension = Path.GetExtension(path: file).ToLower();

                    if (_extensionFilter.Length > 0 && !_extensionFilter.Contains(value: extension))
                        return;

                    bool isVideoFile = extension is ".mp4" or ".avi" or ".mkv" or ".m3u8";
                    bool isAudioFile = extension is ".mp3" or ".flac" or ".wav" or ".m4a";
                    bool isSubtitleFile = extension is ".srt" or ".vtt" or ".ass" or ".sub";

                    if (!isVideoFile && !isAudioFile && !isSubtitleFile)
                        return;

                    MovieFile? movieFile =
                        isVideoFile || isAudioFile ? _movieDetector.GetInfo(filePath: file) : null;

                    // Override MovieDetector's season/episode with our own regex on the filename only,
                    // because MovieDetector can pick up season/episode numbers from parent folder segments
                    // in the full path (e.g. "S02 1080p" in the folder name instead of the file name).
                    if (movieFile is not null && (isVideoFile || isAudioFile))
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path: file);
                        string cleanedFileName = StringExtensions
                            .RemoveBracketedString()
                            .Replace(input: fileNameWithoutExtension, replacement: string.Empty)
                            .Trim();

                        Match epMatch = StringExtensions
                            .MatchEpisodePrefix()
                            .Match(input: cleanedFileName);
                        if (!epMatch.Success)
                            epMatch = StringExtensions.MatchSeasonEpisode().Match(input: cleanedFileName);

                        if (epMatch is { Success: true, Groups.Count: >= 3 })
                        {
                            movieFile.Season = int.Parse(s: epMatch.Groups[groupnum: 1].Value);
                            movieFile.Episode = int.Parse(s: epMatch.Groups[groupnum: 2].Value);
                            movieFile.IsSeries = true;
                            movieFile.IsSuccess = true;
                        }
                        else
                        {
                            Match wordMatch = StringExtensions
                                .MatchEpisodeWord()
                                .Match(input: cleanedFileName);
                            if (wordMatch.Success)
                            {
                                movieFile.Episode = int.Parse(s: wordMatch.Groups[groupnum: 1].Value);
                                movieFile.IsSeries = true;
                                movieFile.IsSuccess = true;
                            }
                        }
                    }

                    FfProbeData? ffprobe = null;
                    TagFile? tagFile = null;
                    try
                    {
                        if (isVideoFile || isAudioFile)
                        {
                            ffprobe = await FfProbe.CreateAsync(driver: _driver, file: file, ct: cancellationToken);
                            if (isAudioFile)
                            {
                                // TagFile needs random access — only safe on local FS.
                                if (_driver is LocalStorageDriver)
                                    tagFile = TagFile.Create(path: file);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.App(message: e.Message, level: LogEventLevel.Fatal);
                    }

                    MovieFileExtend movieFileExtend = new()
                    {
                        FilePath = movieFile?.Path ?? file,
                        Episode = movieFile?.Episode,
                        Year = movieFile?.Year,
                        Season = movieFile?.Season,
                        Title = movieFile?.Title,
                        IsSeries = movieFile?.IsSeries ?? false,
                        IsSuccess = movieFile?.IsSuccess ?? false,
                        DiscNumber = tagFile?.Tag?.Disc.ToInt() ?? 0,
                        TrackNumber = tagFile?.Tag?.Track.ToInt() ?? 0,
                    };

                    MediaFile res = new()
                    {
                        Name = Path.GetFileName(path: file),
                        Path = file,
                        Extension = extension,
                        Size = _driver.GetFileSize(path: file),
                        Created = _driver.GetCreationTimeUtc(path: file).ToLocalTime(),
                        Modified = _driver.GetLastWriteTimeUtc(path: file).ToLocalTime(),
                        Accessed = _driver.GetLastAccessTimeUtc(path: file).ToLocalTime(),
                        Type = "file",

                        Parsed = movieFileExtend,
                        FFprobe = ffprobe,
                        TagFile = tagFile,
                    };

                    files.Add(item: res);
                }
            );

            ConcurrentBag<MediaFile> response = files.OrderBy(keySelector: f => f.Name).ToConcurrentBag();

            return response;
        }
        catch (Exception e)
        {
            Logger.App(message: e.Message, level: LogEventLevel.Fatal);

            return files;
        }
    }

    public void Dispose() { }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public MediaScan FilterByFileName(string? filter)
    {
        Filter = filter;

        return this;
    }
}
