using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;
using Serilog.Events;
using SixLabors.ImageSharp;
using SubtitlesParserV2;
using SubtitlesParserV2.Models;
using Image = SixLabors.ImageSharp.Image;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager(
    IFileRepository fileRepository,
    IStorageFactory storageFactory,
    IStorageDriver storageDriver
) : IFileManager
{
    private readonly IStorageFactory _storageFactory = storageFactory;
    private readonly IStorageDriver _storageDriver = storageDriver;

    private IStorage StorageFor(Folder folder) =>
        _storageFactory.For(folder.Id, folder.DriverId, string.Empty);

    private int Id { get; set; }
    private Movie? Movie { get; set; }
    private Tv? Show { get; set; }

    private List<Folder> Folders { get; set; } = [];
    private List<MediaFolderExtend> Files { get; set; } = [];
    public string Type { get; set; } = "";

    private string? Filter { get; set; }

    public async Task FindFiles(int id, Library library)
    {
        Id = id;

        await MediaType(id, library);

        Folders = Paths(library, Movie, Show);

        foreach (Folder folder in Folders)
        {
            // Pass the whole folder so GetFiles can resolve the right driver
            // (local / NFS / S3) for it. Hardcoding _storageDriver was
            // scanning every library against the local disk regardless of
            // its actual backend — NFS NAS and S3 buckets returned 0 files.
            ConcurrentBag<MediaFolderExtend> files = await GetFiles(library, folder);

            if (!files.IsEmpty)
                Files.AddRange(files);
        }

        // Wrap delete + insert in a transaction so old records are preserved if insert fails
        await using IDbContextTransaction transaction =
            await fileRepository.BeginTransactionAsync();
        try
        {
            // Only clear all existing records during a full rescan (no filter).
            // When a filter is set (e.g. after encoding a single episode), we just upsert
            // the new files without deleting the rest of the show's records.
            if (Filter is null)
            {
                switch (library.Type)
                {
                    case Config.MovieMediaType:
                        await fileRepository.DeleteVideoFilesAndMetadataByMovieIdAsync(id);
                        break;
                    case Config.TvMediaType:
                    case Config.AnimeMediaType:
                        await fileRepository.DeleteVideoFilesAndMetadataByTvIdAsync(Show?.Id ?? id);
                        break;
                }
            }

            switch (library.Type)
            {
                case Config.MovieMediaType:
                    await StoreMovie();
                    break;
                case Config.TvMediaType:
                case Config.AnimeMediaType:
                    await StoreTvShow();
                    break;
                case Config.MusicMediaType:
                    await StoreMusic();
                    break;
                default:
                    Logger.App("Unknown library type");
                    break;
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Publish refresh events only after successful commit
        switch (library.Type)
        {
            case Config.MovieMediaType:
            case Config.TvMediaType:
            case Config.AnimeMediaType:
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshEvent { QueryKey = ["libraries", library.Id.ToString()] }
                    );
                break;
            case Config.MusicMediaType:
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshEvent { QueryKey = ["music"] }
                    );
                break;
        }
    }

    public void FilterFiles(string filter)
    {
        Filter = filter;
    }

    public async Task MoveToLibraryFolder(int id, Folder folder)
    {
        await using MediaContext context = new();

        Tv? tv = await context
            .Tvs.Include(tv => tv.Library)
                .ThenInclude(lib => lib.FolderLibraries)
                    .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(tv => tv.Episodes)
                .ThenInclude(e => e.VideoFiles)
            .FirstOrDefaultAsync(t => t.Id == id);

        Movie? movie = await context
            .Movies.Include(movie => movie.Library)
                .ThenInclude(lib => lib.FolderLibraries)
                    .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(movie => movie.VideoFiles)
            .FirstOrDefaultAsync(movie => movie.Id == id);

        string folderName = "";
        string sourceFolder = "";

        if (tv?.Folder is not null)
            foreach (FolderLibrary libraryFolder in tv.Library.FolderLibraries)
            {
                IStorage folderStorage = StorageFor(libraryFolder.Folder);
                string path = libraryFolder.Folder.Path + tv.Folder;
                if (!folderStorage.Exists(path))
                {
                    string? match = Str.FindMatchingDirectory(
                        _storageDriver,
                        libraryFolder.Folder.Path,
                        tv.Folder.Replace("/", "")
                    );
                    if (match != null)
                        path = match;
                }

                if (!folderStorage.Exists(path))
                    continue;

                folderName = tv.Folder;
                sourceFolder = path;

                break;
            }
        else if (movie?.Folder is not null)
            foreach (FolderLibrary libraryFolder in movie.Library.FolderLibraries)
            {
                IStorage folderStorage = StorageFor(libraryFolder.Folder);
                string path = libraryFolder.Folder.Path + movie.Folder;
                if (!folderStorage.Exists(path))
                {
                    string? match = Str.FindMatchingDirectory(
                        _storageDriver,
                        libraryFolder.Folder.Path,
                        movie.Folder.Replace("/", "")
                    );
                    if (match != null)
                        path = match;
                }

                if (!folderStorage.Exists(path))
                    continue;

                folderName = movie.Folder;
                sourceFolder = path;

                break;
            }

        if (string.IsNullOrEmpty(folderName) || string.IsNullOrEmpty(sourceFolder))
        {
            Logger.App("Folder not found");
            return;
        }

        string destinationFolder = folder.Path + folderName;

        Logger.App($"Moving {sourceFolder} to {destinationFolder}");

        // TODO: cross-backend — if source and destination folders are on different backends
        // this needs AcquireLocalPathAsync + Stream copy semantics. For now both sides are
        // assumed to be on the same storage backend (local), so we resolve source storage from
        // the destination folder (the only folder context available here). A full cross-backend
        // move is tracked as a future task.
        IStorage destinationStorage = StorageFor(folder);
        MoveFolder(sourceFolder, destinationFolder, destinationStorage);

        FolderLibrary? newFolderLibrary = await context
            .FolderLibrary.Include(fl => fl.Library)
            .Include(fl => fl.Folder)
            .FirstOrDefaultAsync(fl => fl.FolderId == folder.Id);

        if (newFolderLibrary is null)
            return;

        if (tv?.Folder is not null)
        {
            tv.Folder = folderName;
            tv.LibraryId = newFolderLibrary.LibraryId;

            LibraryTv? libraryTv = await context.LibraryTv.FirstOrDefaultAsync(lt =>
                lt.TvId == tv.Id
            );

            if (libraryTv is not null)
                libraryTv.LibraryId = newFolderLibrary.LibraryId;

            await context.SaveChangesAsync();
        }
        else if (movie?.Folder is not null)
        {
            movie.Folder = folderName;
            movie.LibraryId = newFolderLibrary.LibraryId;

            LibraryMovie? libraryMovie = await context.LibraryMovie.FirstOrDefaultAsync(lm =>
                lm.MovieId == movie.Id
            );

            if (libraryMovie is not null)
                libraryMovie.LibraryId = newFolderLibrary.LibraryId;

            await context.SaveChangesAsync();
        }

        await FindFiles(id, newFolderLibrary.Library);
    }

    private async Task StoreMusic()
    {
        List<MediaFile> items = Files
            .SelectMany(file => file.Files ?? [])
            .Where(mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        if (items.Count == 0)
            return;

        foreach (MediaFile item in items)
            await StoreAudioItem(item);

        Logger.App($"Found {items.Count} music files");
    }

    private async Task StoreMovie()
    {
        MediaFile? item = Files
            .SelectMany(file => file.Files ?? [])
            .FirstOrDefault(file => file.Parsed is not null);

        if (item == null)
            return;

        await StoreVideoItem(item);

        Logger.App($"Found {item.Path} for {Movie?.Title}");
    }

    private async Task StoreTvShow()
    {
        List<MediaFile> items = Files
            .SelectMany(file => file.Files ?? [])
            .Where(mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        if (items.Count == 0)
            return;

        foreach (MediaFile item in items)
        {
            await StoreVideoItem(item);
        }

        Logger.App($"Found {items.Count} files for {Show?.Title}");
    }

    private async Task StoreAudioItem(MediaFile? item)
    {
        if (item?.Parsed is null)
            return;

        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null)
            return;

        await Task.CompletedTask;
    }

    private async Task StoreVideoItem(MediaFile item)
    {
        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null)
            return;

        IStorage storage = StorageFor(folder);

        string itemPath = item.Path.Replace('\\', '/');
        string fileName = "/" + Path.GetFileName(itemPath);
        string hostFolder = itemPath.Replace(fileName, "");
        string showName = (Movie?.Folder ?? Show?.Folder).OrEmpty().Trim('/', '\\');
        int showIdx = string.IsNullOrEmpty(showName)
            ? -1
            : itemPath.IndexOf(showName, StringComparison.OrdinalIgnoreCase);
        string baseFolder =
            showIdx >= 0 ? ("/" + itemPath[showIdx..]).Replace(fileName, "") : hostFolder;

        List<Subtitle> subtitles = GetSubtitles(storage, hostFolder);

        List<IVideoTrack> tracks = GetExtraFiles(storage, hostFolder);

        Episode? episode = await fileRepository.GetEpisode(Show?.Id, item);

        Metadata metadata = await MakeMetadata(
            storage,
            item,
            fileName,
            baseFolder,
            hostFolder,
            tracks
        );

        Ulid metadataId = await fileRepository.StoreMetadata(metadata);

        Logger.App($"Storing video file: {episode?.Id}, {Movie?.Id}", LogEventLevel.Verbose);
        VideoFile videoFile = new()
        {
            EpisodeId = episode?.Id,
            MovieId = Movie?.Id,
            Folder = baseFolder.Replace("\\", "/"),
            HostFolder = hostFolder.Replace("\\", "/"),
            Filename = fileName.Replace("\\", "/"),

            Share = folder.Id.ToString(),
            Duration = Regex.Replace(
                Regex.Replace((item.FFprobe?.Duration.ToString()).OrEmpty(), "\\.\\d+", ""),
                "^00:",
                ""
            ),
            // Chapters = JsonConvert.SerializeObject(item.FFprobe?.Chapters ?? []),
            Chapters = "",
            Languages = JsonConvert.SerializeObject(
                item.FFprobe?.AudioStreams.Select(stream => stream.Language)
                    .Where(stream => stream != null && stream != "und")
            ),
            Quality = (item.FFprobe?.VideoStreams.FirstOrDefault()?.Width.ToString()).OrEmpty(),
            Subtitles = JsonConvert.SerializeObject(subtitles),
            Tracks = tracks.ToArray(),
            MetadataId = metadataId,
        };

        await fileRepository.StoreVideoFile(videoFile);
    }

    private async Task<Metadata> MakeMetadata(
        IStorage storage,
        MediaFile item,
        string fileName,
        string baseFolder,
        string hostFolder,
        List<IVideoTrack> extraFiles
    )
    {
        List<IVideo> video = GetVideoHashList(storage, hostFolder);
        List<IAudio> audio = GetAudioHashList(storage, hostFolder);
        List<ISubtitle> subtitles = GetSubtitleHashList(storage, hostFolder);
        List<IFont> fonts = GetFontHashList(storage, hostFolder);
        List<IPreview> previews = GetPreviewHashList(storage, hostFolder, extraFiles);

        IVideoTrack? chaptersFile = extraFiles.FirstOrDefault(file => file.Kind == "chapters");

        List<IChapter> chapters = chaptersFile?.File is not null
            ? await GetChapterHashListAsync(
                storage,
                hostFolder,
                Path.GetFileName(chaptersFile.File)
            )
            : [];

        IChapterFile? chaptersFileHashMap = chaptersFile?.File is not null
            ? new()
            {
                FileName = "/" + Path.GetFileName(chaptersFile.File).Replace("\\", "/"),
                FileSize = storage.SizeOrZero(
                    storage.CombinePath(hostFolder, Path.GetFileName(chaptersFile.File))
                ),
                FileHash = ComputeFileHash(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(chaptersFile.File))
                ),
            }
            : null;

        IFontsFile? fontsFileHashMap = extraFiles
            .Where(file => file.Kind == "fonts")
            .Select(file => new IFontsFile
            {
                FileName = "/" + Path.GetFileName(file.File).Replace("\\", "/"),
                FileSize = storage.SizeOrZero(
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
                FileHash = ComputeFileHash(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
            })
            .FirstOrDefault();

        Metadata metadata = new()
        {
            Filename = fileName.Replace("\\", "/"),
            Duration = (item.FFprobe?.Duration.ToString()).OrEmpty(),
            Folder = baseFolder.Replace("\\", "/"),
            HostFolder = hostFolder.Replace("\\", "/"),
            FolderSize = GetDirectorySize(storage, hostFolder),

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

    private List<IVideo> GetVideoHashList(IStorage storage, string hostFolder)
    {
        List<IVideo> videos = [];

        if (!storage.Exists(hostFolder))
            return videos;

        // V3 encoder creates directories like video_1920x1080/ with .m3u8 playlist inside
        foreach (
            StorageEntry dir in storage
                .List(hostFolder, null, recursive: false)
                .Where(e => e.IsDirectory && Path.GetFileName(e.Path).StartsWith("video_"))
        )
        {
            string dirName = Path.GetFileName(dir.Path);
            Match match = VideoDirectoryRegex().Match(dirName);
            if (!match.Success)
                continue;

            int width = int.Parse(match.Groups["width"].Value);
            int height = int.Parse(match.Groups["height"].Value);

            IReadOnlyList<StorageEntry> playlists = storage.List(
                dir.Path,
                "*.m3u8",
                recursive: false
            );
            if (playlists.Count == 0)
                continue;

            string playlistPath = playlists[0].Path;
            long dirSize = storage
                .List(dir.Path, null, recursive: false)
                .Where(e => !e.IsDirectory)
                .Sum(e => e.SizeBytes);

            videos.Add(
                new()
                {
                    Width = width,
                    Height = height,
                    FileName =
                        "/" + Path.GetRelativePath(hostFolder, playlistPath).Replace("\\", "/"),
                    FileHash = ComputeFileHash(storage, playlistPath),
                    FileSize = dirSize,
                }
            );
        }

        return videos;
    }

    private List<IAudio> GetAudioHashList(IStorage storage, string hostFolder)
    {
        List<IAudio> audioList = [];

        if (!storage.Exists(hostFolder))
            return audioList;

        // Encoder creates directories like audio_eng_aac/ with .m3u8 playlist inside
        foreach (
            StorageEntry dir in storage
                .List(hostFolder, null, recursive: false)
                .Where(e => e.IsDirectory && Path.GetFileName(e.Path).StartsWith("audio_"))
        )
        {
            string dirName = Path.GetFileName(dir.Path);
            Match match = AudioDirectoryRegex().Match(dirName);
            if (!match.Success)
                continue;

            string language = match.Groups["lang"].Value;
            string codec = match.Groups["codec"].Value;

            IReadOnlyList<StorageEntry> playlists = storage.List(
                dir.Path,
                "*.m3u8",
                recursive: false
            );
            if (playlists.Count == 0)
                continue;

            string playlistPath = playlists[0].Path;
            long dirSize = storage
                .List(dir.Path, null, recursive: false)
                .Where(e => !e.IsDirectory)
                .Sum(e => e.SizeBytes);

            audioList.Add(
                new()
                {
                    Language = language,
                    Codec = codec,
                    FileName =
                        "/" + Path.GetRelativePath(hostFolder, playlistPath).Replace("\\", "/"),
                    FileHash = ComputeFileHash(storage, playlistPath),
                    FileSize = dirSize,
                }
            );
        }

        return audioList;
    }

    private List<ISubtitle> GetSubtitleHashList(IStorage storage, string hostFolder)
    {
        List<ISubtitle> subtitles = [];

        string subtitleFolder = storage.CombinePath(hostFolder, "subtitles");

        if (!storage.Exists(subtitleFolder))
            return subtitles;

        IReadOnlyList<StorageEntry> subtitleFiles = storage.List(
            subtitleFolder,
            null,
            recursive: false
        );
        foreach (StorageEntry subtitleEntry in subtitleFiles.Where(e => !e.IsDirectory))
        {
            Regex regex = SubtitleFileRegex();
            Match match = regex.Match(subtitleEntry.Path);

            if (!match.Success)
                continue;

            string path = storage.CombinePath(hostFolder, subtitleEntry.Path);

            // Reject binary subtitle formats we can't stream as HLS sidecars; accept
            // every text format (vtt, ass, srt, ssa, sub, idx, webvtt).
            string ext = match.Groups["ext"].Value;
            if (ext == "sup" || ext == "vob")
                continue;

            subtitles.Add(
                new()
                {
                    Language = match.Groups["lang"].Value,
                    Type = match.Groups["type"].Value,
                    FileName =
                        "/" + Path.Combine("subtitles", Path.GetFileName(path)).Replace("\\", "/"),
                    FileHash = ComputeFileHash(storage, path),
                    FileSize = subtitleEntry.SizeBytes,
                    Codec = ext,
                }
            );
        }

        return subtitles;
    }

    private List<IPreview> GetPreviewHashList(
        IStorage storage,
        string hostFolder,
        List<IVideoTrack> extraFiles
    )
    {
        IEnumerable<IPreview> sprites = extraFiles
            .Where(file => file.Kind == "sprite")
            .Select(file => new IPreview
            {
                ImageFileName = "/" + (Path.GetFileName(file.File).OrEmpty()).Replace("\\", "/"),
                ImageFileSize = storage.SizeOrZero(
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
                ImageFileHash = ComputeFileHash(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
            });

        IEnumerable<IPreview> times = extraFiles
            .Where(file => file.Kind == "thumbnails")
            .Select(file => new IPreview
            {
                Width = GetImageDimensionsFromVtt(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ).Width,
                Height = GetImageDimensionsFromVtt(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ).Height,
                TimeFileName = "/" + (Path.GetFileName(file.File).OrEmpty()).Replace("\\", "/"),
                TimeFileSize = storage.SizeOrZero(
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
                TimeFileHash = ComputeFileHash(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
            });

        List<IPreview> previews = sprites
            .Zip(
                times,
                (sprite, time) =>
                    new IPreview
                    {
                        Width = time.Width,
                        Height = time.Height,
                        ImageFileName = sprite.ImageFileName,
                        ImageFileSize = sprite.ImageFileSize,
                        ImageFileHash = sprite.ImageFileHash,
                        TimeFileName = time.TimeFileName,
                        TimeFileSize = time.TimeFileSize,
                        TimeFileHash = time.TimeFileHash,
                    }
            )
            .ToList();
        return previews;
    }

    private List<IFont> GetFontHashList(IStorage storage, string hostFolder)
    {
        string fontFolder = storage.CombinePath(hostFolder, "fonts");

        List<IFont> fonts = [];

        if (!storage.Exists(fontFolder))
            return fonts;

        IReadOnlyList<StorageEntry> fontFiles = storage.List(fontFolder, null, recursive: false);
        foreach (StorageEntry fontEntry in fontFiles.Where(e => !e.IsDirectory))
        {
            string path = storage.CombinePath(hostFolder, fontEntry.Path);
            fonts.Add(
                new()
                {
                    FileName =
                        "/" + Path.Combine("fonts", Path.GetFileName(path)).Replace("\\", "/"),
                    FileHash = ComputeFileHash(storage, path),
                    FileSize = fontEntry.SizeBytes,
                }
            );
        }

        return fonts;
    }

    private async Task<List<IChapter>> GetChapterHashListAsync(
        IStorage storage,
        string hostFolder,
        string file
    )
    {
        string path = storage.CombinePath(hostFolder, file);

        List<IChapter> chapters = [];

        List<IChapter>? parsedChapters = await ParseChaptersAsync(storage, path);

        foreach (IChapter parsedChapter in parsedChapters ?? [])
        {
            chapters.Add(
                new()
                {
                    EndTime = parsedChapter.EndTime,
                    StartTime = parsedChapter.StartTime,
                    Title = parsedChapter.Title,
                    Id = parsedChapter.Id,
                }
            );
        }

        return chapters;
    }

    private long GetDirectorySize(IStorage storage, string folder)
    {
        if (!storage.Exists(folder))
            return 0;

        IReadOnlyList<StorageEntry> entries = storage.List(folder, null, recursive: true);
        return entries.Where(e => !e.IsDirectory).Sum(e => e.SizeBytes);
    }

    private static void MoveFolder(string sourceFolder, string destinationFolder, IStorage storage)
    {
        if (storage.Exists(sourceFolder))
        {
            storage.MoveDirectory(sourceFolder, destinationFolder);

            Logger.App($"Moved {sourceFolder} to {destinationFolder}");
        }
        else
        {
            throw new DirectoryNotFoundException($"Source folder not found: {sourceFolder}");
        }
    }

    private static (int Width, int Height) GetImageDimensions(string filePath)
    {
        ImageInfo info = Image.Identify(filePath);

        return (info.Width, info.Height);
    }

    private static (int Width, int Height) GetImageDimensionsFromVtt(
        IStorage storage,
        string filePath
    )
    {
        string vttContents = storage
            .ReadAllTextAsync(filePath, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Regex regex = ImageDimensions();
        Match match = regex.Match(vttContents);

        if (match.Success)
        {
            int width = int.Parse(match.Groups["width"].Value);
            int height = int.Parse(match.Groups["height"].Value);
            return (width, height);
        }

        return (0, 0);
    }

    private static async Task<List<IChapter>?> ParseChaptersAsync(
        IStorage storage,
        string chapterFile
    )
    {
        await using Stream fileStream = storage.OpenRead(chapterFile);

        SubtitleParserResultModel? chapterParser = SubtitleParser.ParseStream(fileStream);

        if (chapterParser?.Subtitles == null || chapterParser.Subtitles.Count == 0)
            return null;

        List<IChapter> chapters = [];

        foreach (SubtitleModel subtitleParserResult in chapterParser.Subtitles)
        {
            if (subtitleParserResult?.StartTime == null || subtitleParserResult?.EndTime == null)
            {
                Logger.App($"Invalid chapter time in {chapterFile}", LogEventLevel.Warning);
                continue;
            }

            IChapter chapter = new()
            {
                Id = chapterParser.Subtitles.IndexOf(subtitleParserResult),
                StartTime = subtitleParserResult.StartTime,
                EndTime = subtitleParserResult.EndTime,
                Title = subtitleParserResult.Lines.First(),
            };

            chapters.Add(chapter);
        }

        return chapters;
    }

    private static T? GetMetaDataItem<T>(
        IStorage storage,
        string hostFolder,
        string key,
        IEnumerable<IVideoTrack> extraFiles
    )
        where T : class
    {
        IVideoTrack? item = extraFiles.FirstOrDefault(file => file.Kind == key);
        if (item == null)
            return null;

        string path = storage.CombinePath(
            hostFolder,
            Path.GetFileName(item.File).OrEmpty().Replace("/", "")
        );
        return new IHash
            {
                FileName = "/" + Path.GetFileName(item.File),
                FileSize = storage.SizeOrZero(path),
                FileHash = ComputeFileHash(storage, path),
            } as T;
    }

    private static List<IVideoTrack> GetExtraFiles(IStorage storage, string hostFolder)
    {
        List<IVideoTrack> tracks = [];

        IReadOnlyList<StorageEntry> files = storage.List(hostFolder, null, recursive: false);

        // Index every thumb/sprite candidate first so we can pair a VTT with
        // a same-stem WEBP. Stale VTT files from a previous re-encode (e.g.
        // thumbs_320x178.vtt) used to be registered alongside the live sprite
        // (thumbs_320x180.webp), and the player followed the VTT's cues to a
        // non-existent webp — 404 every hover.
        Dictionary<string, string> spriteByStem = new(StringComparer.OrdinalIgnoreCase);
        List<(string Name, string Stem)> vttCandidates = [];

        foreach (StorageEntry entry in files.Where(e => !e.IsDirectory))
        {
            string name = Path.GetFileName(entry.Path);
            string stem = Path.GetFileNameWithoutExtension(name);

            if (name.StartsWith("chapter"))
                tracks.Add(new() { File = "/" + name, Kind = "chapters" });
            else if (name.StartsWith("skipper"))
                tracks.Add(new() { File = "/" + name, Kind = "skippers" });
            else if (
                (
                    name.StartsWith("sprite")
                    || name.StartsWith("preview")
                    || name.StartsWith("thumb")
                ) && entry.Path.EndsWith("vtt")
            )
                vttCandidates.Add((name, stem));
            else if (
                (name.StartsWith("sprite") || name.StartsWith("thumb"))
                && entry.Path.EndsWith("webp")
            )
            {
                spriteByStem[stem] = name;
                tracks.Add(new() { File = "/" + name, Kind = "sprite" });
            }
            else if (name.StartsWith("fonts"))
                tracks.Add(new() { File = "/" + name, Kind = "fonts" });
        }

        // Only register VTTs whose basename has a matching sprite WEBP on
        // disk. Drops stale VTTs left behind when the sprite was re-rendered
        // at a different dimension and the old VTT wasn't cleaned up.
        foreach ((string name, string stem) in vttCandidates)
        {
            if (spriteByStem.ContainsKey(stem))
                tracks.Add(new() { File = "/" + name, Kind = "thumbnails" });
        }

        return tracks;
    }

    private static List<Subtitle> GetSubtitles(IStorage storage, string hostFolder)
    {
        string subtitleFolder = storage.CombinePath(hostFolder, "subtitles");

        List<Subtitle> subtitles = [];

        if (!storage.Exists(subtitleFolder))
            return subtitles;

        IReadOnlyList<StorageEntry> subtitleFiles = storage.List(
            subtitleFolder,
            null,
            recursive: false
        );
        foreach (StorageEntry subtitleEntry in subtitleFiles.Where(e => !e.IsDirectory))
        {
            Regex regex = SubtitleFileRegex();
            Match match = regex.Match(subtitleEntry.Path);

            if (!match.Success)
                continue;

            // Reject binary subtitle formats; accept every text format.
            string ext = match.Groups["ext"].Value;
            if (ext == "sup" || ext == "vob")
                continue;

            subtitles.Add(
                new()
                {
                    Language = match.Groups["lang"].Value,
                    Type = match.Groups["type"].Value,
                    Ext = ext,
                }
            );
        }

        return subtitles;
    }

    private async Task MediaType(int id, Library library)
    {
        (Movie, Show, Type) = await fileRepository.MediaType(id, library);
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> GetFiles(Library library, Folder folder)
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = _storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        MediaScan mediaScan = new(folderStorage.Driver);

        int depth = library.Type switch
        {
            Config.MovieMediaType => 1,
            Config.TvMediaType or Config.AnimeMediaType => 2,
            _ => 0,
        };

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .FilterByMediaType(library.Type)
            .FilterByFileName(Filter)
            .Process(folder.Path, depth);

        await mediaScan.DisposeAsync();

        return folders;
    }

    private List<Folder> Paths(Library library, Movie? movie = null, Tv? show = null)
    {
        List<Folder> folders = [];
        string? folder = library.Type switch
        {
            Config.MovieMediaType => movie?.Folder?.Replace("/", ""),
            Config.TvMediaType or Config.AnimeMediaType => show?.Folder?.Replace("/", ""),
            _ => "",
        };

        if (folder == null)
        {
            Logger.App("Folder not set");
            return folders;
        }

        using MediaContext mediaContext = new();
        Folder[] rootFolders = mediaContext
            .FolderLibrary.Include(fl => fl.Folder)
                .ThenInclude(fl => fl.Driver)
            .Select(f => f.Folder)
            .ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            IStorage folderStorage = StorageFor(rootFolder);
            string path = folderStorage.CombinePath(rootFolder.Path, folder);

            if (!folderStorage.Exists(path))
            {
                string? match = Str.FindMatchingDirectory(_storageDriver, rootFolder.Path, folder);
                if (match != null)
                    path = match;
            }

            if (folderStorage.Exists(path))
                folders.Add(
                    new()
                    {
                        Path = path,
                        Id = rootFolder.Id,
                        DriverId = rootFolder.DriverId,
                        Driver = rootFolder.Driver,
                    }
                );
        }

        return folders;
    }

    private static string ComputeFileHash(IStorage storage, string filePath)
    {
        using SHA256 sha256 = SHA256.Create();
        using Stream fileStream = storage.OpenRead(filePath);

        byte[] hashBytes = sha256.ComputeHash(fileStream);

        StringBuilder hashStringBuilder = new(64);

        foreach (byte b in hashBytes)
            hashStringBuilder.Append(b.ToString("x2"));
        return hashStringBuilder.ToString();
    }

    // Liberal subtitle filename matcher: 2-3 char language (ISO 639-1 or -3), arbitrary
    // type (alt, full, sign, song, sdh, forced, commentary, director, ...), 3-6 char
    // extension (vtt, ass, srt, ssa, sub, idx, sup, vob, webvtt).  Previously the regex
    // only matched 3-char lang + 3-4 char type + 3-char ext and the consumer methods
    // further filtered to type in (sign, song, full) — both combined silently dropped
    // every "alt" / "sdh" / "forced" subtitle file.
    [GeneratedRegex(@"(?<lang>[a-zA-Z]{2,3})\.(?<type>\w+)\.(?<ext>\w{3,6})$")]
    private static partial Regex SubtitleFileRegex();

    [GeneratedRegex(@"#xywh=\d+,\d+,(?<width>\d+),(?<height>\d+)")]
    private static partial Regex ImageDimensions();

    [GeneratedRegex(@"^video_(?<width>\d+)x(?<height>\d+)(?:_(?:SDR|HDR))?$")]
    private static partial Regex VideoDirectoryRegex();

    [GeneratedRegex(@"^audio_(?<lang>\w+)_(?<codec>\w+)$")]
    private static partial Regex AudioDirectoryRegex();
}
