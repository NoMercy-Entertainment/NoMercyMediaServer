using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MovieFileLibrary;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem;

public class MediaScan : IDisposable, IAsyncDisposable
{
    private readonly MovieDetector _movieDetector = new();

    private bool _fileListingEnabled;
    private bool _regexFilterEnabled = true;
    private string? Filter { get; set; }
    
    private readonly Regex _folderNameRegex =
        new(
            @"video_.*|audio_.*|subtitles|scans|cds.*|ost|album|music|original|fonts|thumbs|metadata|NCED|NCOP|\s\(\d\)\.|~",
            RegexOptions.IgnoreCase);

    private string[] _extensionFilter = [];


    public MediaScan()
    {
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
            "anime" or Config.TvMediaType or Config.MovieMediaType or "video" => [".mp4", ".avi", ".mkv", ".m3u8"],
            "music" => [".mp3", ".flac", ".wav", ".m4a"],
            "subtitle" => [".srt", ".vtt", ".ass"],
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null)
        };

        return this;
    }

    public async Task<ConcurrentBag<MediaFolderExtend>> Process(string rootFolder, int depth = 0)
    {
        rootFolder = Path.GetFullPath(rootFolder);
        return !_fileListingEnabled
            ? await Task.Run(() => ScanFoldersOnly(rootFolder, depth))
            : await ScanFolderAsync(rootFolder, depth);
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> ScanFolderAsync(string folderPath, int depth)
    {
        folderPath = Path.GetFullPath(folderPath);

        ConcurrentBag<MediaFolderExtend> folders = [];

        if (depth < 0) return folders;

        // Ensure the path is a directory before proceeding
        if (!Directory.Exists(folderPath))
            return folders;

        ConcurrentBag<MediaFile> files = await FilesAsync(folderPath);

        MovieFile movieFile1 = _movieDetector.GetInfo(folderPath);
        movieFile1.Year ??= folderPath.TryGetYear();

        folders.Add(new()
        {
            Name = Path.GetFileName(folderPath),
            Path = folderPath,
            Created = Directory.GetCreationTime(folderPath),
            Modified = Directory.GetLastWriteTime(folderPath),
            Accessed = Directory.GetLastAccessTime(folderPath),
            Type = "folder",
            Parsed = new()
            {
                Title = movieFile1.Title,
                Year = movieFile1.Year,
                FilePath = movieFile1.Path
            },

            Files = files
        });

        try
        {
            IOrderedEnumerable<string>? directories = null;
            try
            {
                directories = Directory.GetDirectories(folderPath).OrderBy(f => f);
            }
            catch
            {
                // ignored
            }
            if (directories is null) return folders;

            await Parallel.ForEachAsync(directories, Config.ParallelOptions, async (directory, cancellationToken) =>
            {
                string folderName = Path.GetFileName(directory);

                if ((_regexFilterEnabled && _folderNameRegex.IsMatch(folderName)) || depth == 0)
                {
                    files.Add(new()
                    {
                        Name = folderName,
                        Path = directory,
                        Created = Directory.GetCreationTime(directory),
                        Modified = Directory.GetLastWriteTime(directory),
                        Accessed = Directory.GetLastAccessTime(directory),
                        Type = "folder"
                    });

                    return;
                }

                ConcurrentBag<MediaFile> files2 = depth - 1 > 0 ? await FilesAsync(directory) : [];

                MovieFile movieFile = _movieDetector.GetInfo(directory);
                movieFile.Year ??= directory.TryGetYear();

                folders.Add(new()
                {
                    Name = folderName,
                    Path = directory,
                    Created = Directory.GetCreationTime(directory),
                    Modified = Directory.GetLastWriteTime(directory),
                    Accessed = Directory.GetLastAccessTime(directory),
                    Type = "folder",
                    Parsed = new()
                    {
                        Title = movieFile.Title,
                        Year = movieFile.Year,
                        FilePath = movieFile.Path
                    },

                    Files = files2.Count > 0
                        ? files2
                        : null,

                    SubFolders = depth - 1 > 0
                        ? await ScanFolderAsync(directory, depth - 1)
                        : []
                });
            });

            ConcurrentBag<MediaFolderExtend> response = folders
                .Where(f => f.Name is not "")
                .OrderByDescending(f => f.Name)
                .ToConcurrentBag();

            return response;
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Fatal);
            throw;
        }
    }

    private ConcurrentBag<MediaFolderExtend> ScanFoldersOnly(string folderPath, int depth)
    {
        folderPath = Path.GetFullPath(folderPath.ToUtf8());

        if (depth < 0) return [];

        try
        {
            ConcurrentBag<MediaFolderExtend> folders = [];

            IOrderedEnumerable<string> directories = Directory.GetDirectories(folderPath).OrderBy(f => f);

            Parallel.ForEach(directories, Config.ParallelOptions, (directory, _) =>
            {
                string dir = Path.GetFullPath(directory.ToUtf8());
                Logger.App($"Scanning {dir}");

                string folderName = Path.GetFileName(dir);

                if (_regexFilterEnabled && _folderNameRegex.IsMatch(folderName))
                {
                    folders.Add(new()
                    {
                        Name = folderName,
                        Path = dir,
                        Created = Directory.GetCreationTime(dir),
                        Modified = Directory.GetLastWriteTime(dir),
                        Accessed = Directory.GetLastAccessTime(dir),
                        Type = "folder"
                    });

                    return;
                }

                MovieFile movieFile = _movieDetector.GetInfo(directory);

                movieFile.Year ??= directory.TryGetYear();

                folders.Add(new()
                {
                    Name = folderName,
                    Path = directory,
                    Created = Directory.GetCreationTime(directory),
                    Modified = Directory.GetLastWriteTime(directory),
                    Accessed = Directory.GetLastAccessTime(directory),
                    Type = "folder",

                    Parsed = new()
                    {
                        Title = movieFile.Title,
                        Year = movieFile.Year,
                        FilePath = movieFile.Path
                    },

                    SubFolders = depth - 1 > 0
                        ? ScanFoldersOnly(directory, depth - 1)
                        : []
                });
            });

            ConcurrentBag<MediaFolderExtend> response = folders
                .Where(f => f.Name is not "")
                .OrderByDescending(f => f.Name)
                .ToConcurrentBag();

            return response;
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Fatal);
            throw;
        }
    }

    private async Task<ConcurrentBag<MediaFile>> FilesAsync(string folderPath)
    {
        ConcurrentBag<MediaFile> files = [];
        try
        {
            await Parallel.ForEachAsync(Directory.GetFiles(folderPath), Config.ParallelOptions, async (file, cancellationToken) =>
            {
                file = Path.GetFullPath(file.ToUtf8());

                if (Filter is not null)
                    if (!file.Contains(Filter))
                        return;

                string extension = Path.GetExtension(file).ToLower();

                if (_extensionFilter.Length > 0 && !_extensionFilter.Contains(extension)) return;

                bool isVideoFile = extension is ".mp4" or ".avi" or ".mkv" or ".m3u8";
                bool isAudioFile = extension is ".mp3" or ".flac" or ".wav" or ".m4a";
                bool isSubtitleFile = extension is ".srt" or ".vtt" or ".ass" or ".sub";

                if (!isVideoFile && !isAudioFile && !isSubtitleFile) return;

                MovieFile? movieFile = isVideoFile || isAudioFile ? _movieDetector.GetInfo(file) : null;

                FfProbeData? ffprobe = null;
                TagFile? tagFile = null;
                try
                {
                    if (isVideoFile || isAudioFile)
                    {
                        ffprobe = await FfProbe.CreateAsync(file, cancellationToken);
                        if (isAudioFile)
                            tagFile = TagFile.Create(file);
                    }
                }
                catch (Exception e)
                {
                    Logger.App(e.Message, LogEventLevel.Fatal);
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
                    TrackNumber = tagFile?.Tag?.Track.ToInt() ?? 0
                };


                MediaFile res = new()
                {
                    Name = Path.GetFileName(file),
                    Path = file,
                    Extension = extension,
                    Size = (int)new FileInfo(file).Length,
                    Created = File.GetCreationTime(file),
                    Modified = File.GetLastWriteTime(file),
                    Accessed = File.GetLastAccessTime(file),
                    Type = "file",

                    Parsed = movieFileExtend,
                    FFprobe = ffprobe,
                    TagFile = tagFile
                };

                files.Add(res);
            });

            ConcurrentBag<MediaFile> response = files
                .OrderBy(f => f.Name)
                .ToConcurrentBag();

            return response;
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Fatal);

            return files;
        }
    }

    public void Dispose()
    {
    }

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