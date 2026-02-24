using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder;
using NoMercy.Encoder.Dto;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using Serilog.Events;
using SubtitlesParserV2;
using SubtitlesParserV2.Models;
using Image = SixLabors.ImageSharp.Image;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager(
    IFileRepository fileRepository
) : IFileManager
{
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

        // Remove all existing file records to avoid lingering stale entries
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

        Folders = Paths(library, Movie, Show);

        foreach (Folder folder in Folders)
        {
            ConcurrentBag<MediaFolderExtend> files = await GetFiles(library, folder.Path);

            if (!files.IsEmpty) Files.AddRange(files);
        }

        switch (library.Type)
        {
            case Config.MovieMediaType:
                await StoreMovie();
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
                    {
                        QueryKey = ["libraries", library.Id.ToString()]
                    });
                break;
            case Config.TvMediaType:
            case Config.AnimeMediaType:
                await StoreTvShow();
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
                    {
                        QueryKey = ["libraries", library.Id.ToString()]
                    });
                break;
            case Config.MusicMediaType:
                await StoreMusic();
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
                    {
                        QueryKey = ["music"]
                    });
                break;
            default:
                Logger.App("Unknown library type");
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

        Tv? tv = await context.Tvs
            .Include(tv => tv.Library)
            .ThenInclude(lib => lib.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(tv => tv.Episodes)
            .ThenInclude(e => e.VideoFiles)
            .FirstOrDefaultAsync(t => t.Id == id);

        Movie? movie = await context.Movies
            .Include(movie => movie.Library)
            .ThenInclude(lib => lib.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(movie => movie.VideoFiles)
            .FirstOrDefaultAsync(movie => movie.Id == id);

        string folderName = "";
        string sourceFolder = "";

        if (tv?.Folder is not null)
            foreach (FolderLibrary libraryFolder in tv.Library.FolderLibraries)
            {
                string path = libraryFolder.Folder.Path + tv.Folder;
                if (!Directory.Exists(path))
                {
                    string? match = Str.FindMatchingDirectory(libraryFolder.Folder.Path, tv.Folder.Replace("/", ""));
                    if (match != null)
                        path = match;
                }

                if (!Directory.Exists(path)) continue;

                folderName = tv.Folder;
                sourceFolder = path;

                break;
            }
        else if (movie?.Folder is not null)
            foreach (FolderLibrary libraryFolder in movie.Library.FolderLibraries)
            {
                string path = libraryFolder.Folder.Path + movie.Folder;
                if (!Directory.Exists(path))
                {
                    string? match = Str.FindMatchingDirectory(libraryFolder.Folder.Path, movie.Folder.Replace("/", ""));
                    if (match != null)
                        path = match;
                }

                if (!Directory.Exists(path)) continue;

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

        MoveFolder(sourceFolder, destinationFolder);

        FolderLibrary? newFolderLibrary = await context.FolderLibrary
            .Include(fl => fl.Library)
            .Include(fl => fl.Folder)
            .FirstOrDefaultAsync(fl => fl.FolderId == folder.Id);

        if (newFolderLibrary is null) return;

        if (tv?.Folder is not null)
        {
            tv.Folder = folderName;
            tv.LibraryId = newFolderLibrary.LibraryId;

            LibraryTv? libraryTv = await context.LibraryTv
                .FirstOrDefaultAsync(lt => lt.TvId == tv.Id);

            if (libraryTv is not null) libraryTv.LibraryId = newFolderLibrary.LibraryId;

            await context.SaveChangesAsync();
        }
        else if (movie?.Folder is not null)
        {
            movie.Folder = folderName;
            movie.LibraryId = newFolderLibrary.LibraryId;

            LibraryMovie? libraryMovie = await context.LibraryMovie
                .FirstOrDefaultAsync(lm => lm.MovieId == movie.Id);

            if (libraryMovie is not null) libraryMovie.LibraryId = newFolderLibrary.LibraryId;

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

        if (items.Count == 0) return;

        foreach (MediaFile item in items) await StoreAudioItem(item);

        Logger.App($"Found {items.Count} music files");
    }

    private async Task StoreMovie()
    {
        MediaFile? item = Files
            .SelectMany(file => file.Files ?? [])
            .FirstOrDefault(file => file.Parsed is not null);

        if (item == null) return;

        await StoreVideoItem(item);

        Logger.App($"Found {item.Path} for {Movie?.Title}");
    }

    private async Task StoreTvShow()
    {
        List<MediaFile> items = Files
            .SelectMany(file => file.Files ?? [])
            .Where(mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        if (items.Count == 0) return;

        foreach (MediaFile item in items)
        {
            await StoreVideoItem(item);
        }

        Logger.App($"Found {items.Count} files for {Show?.Title}");
    }

    private async Task StoreAudioItem(MediaFile? item)
    {
        if (item?.Parsed is null) return;

        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null) return;

        await Task.CompletedTask;
    }

    private async Task StoreVideoItem(MediaFile item)
    {
        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null) return;

        try
        {
            string fileName = Path.DirectorySeparatorChar + Path.GetFileName(item.Path);
            string hostFolder = item.Path.Replace(fileName, "");
            string baseFolder = (Path.DirectorySeparatorChar + (Movie?.Folder ?? Show?.Folder ?? "").Replace("/", "")
                                                             + item.Path.Replace(folder.Path, "")).Replace(fileName, "");

            List<Subtitle> subtitles = GetSubtitles(hostFolder);

            List<IVideoTrack> tracks = GetExtraFiles(hostFolder);

            Episode? episode = await fileRepository.GetEpisode(Show?.Id, item);

            Metadata metadata = await MakeMetadata(item, fileName, baseFolder, hostFolder, tracks);

            Ulid metadataId = await fileRepository.StoreMetadata(metadata);

            Logger.App($"Storing video file: {episode?.Id}, {Movie?.Id}", LogEventLevel.Verbose);
            VideoFile videoFile = new()
            {
                EpisodeId = episode?.Id,
                MovieId = Movie?.Id,
                Folder = baseFolder.Replace("\\", "/"),
                HostFolder = hostFolder.Replace("\\", "/"),
                Filename = fileName.Replace("\\", "/"),

                Share = folder.Id.ToString() ?? "",
                Duration = Regex.Replace(
                    Regex.Replace(item.FFprobe?.Duration.ToString() ?? ""
                        , "\\.\\d+", ""), "^00:", ""),
                // Chapters = JsonConvert.SerializeObject(item.FFprobe?.Chapters ?? []),
                Chapters = "",
                Languages = JsonConvert.SerializeObject(item.FFprobe?.AudioStreams.Select(stream => stream.Language)
                    .Where(stream => stream != null && stream != "und")),
                Quality = item.FFprobe?.VideoStreams.FirstOrDefault()?.Width.ToString() ?? "",
                Subtitles = JsonConvert.SerializeObject(subtitles),
                Tracks = tracks.ToArray(),
                MetadataId = metadataId
            };

            await fileRepository.StoreVideoFile(videoFile);
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
        }
    }

    private async Task<Metadata> MakeMetadata(MediaFile item, string fileName, string baseFolder, string hostFolder,
        List<IVideoTrack> extraFiles)
    {
        string path = Path.Combine(hostFolder, fileName.Replace("\\", "").Replace("/", ""));
        Ffprobe ffprobeData = await new Ffprobe(fileName).GetStreamData();
        
        List<IVideo> video = GetVideoHashList(hostFolder, ffprobeData);
        List<IAudio> audio = GetAudioHashList(hostFolder, ffprobeData);
        List<ISubtitle> subtitles = GetSubtitleHashList(hostFolder);
        List<IFont> fonts = GetFontHashList(hostFolder);
        List<IPreview> previews = GetPreviewHashList(hostFolder, extraFiles);
        
        IVideoTrack? chaptersFile = extraFiles.FirstOrDefault(file => file.Kind == "chapters");
        
        List<IChapter> chapters = chaptersFile?.File is not null 
            ? GetChapterHashList(hostFolder, Path.GetFileName(chaptersFile.File))
            : [];
        
        IChapterFile? chaptersFileHashMap = chaptersFile?.File is not null
            ? new()
                {
                    FileName = "/" + Path.GetFileName(chaptersFile.File).Replace("\\", "/"),
                    FileSize = GetFileSize(Path.Combine(hostFolder, Path.GetFileName(chaptersFile.File))),
                    FileHash = ComputeFileHash(Path.Combine(hostFolder, Path.GetFileName(chaptersFile.File)))
                }
            : null;
        
        IFontsFile? fontsFileHashMap = extraFiles.Where(file => file.Kind == "fonts")
            .Select(file => new IFontsFile
            {
                FileName = "/" + Path.GetFileName(file.File).Replace("\\", "/"),
                FileSize = GetFileSize(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
                FileHash = ComputeFileHash(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? ""))
            }).FirstOrDefault();

        Metadata metadata = new()
        {
            Filename = fileName.Replace("\\", "/"),
            Duration = item.FFprobe?.Duration.ToString() ?? "",
            Folder = baseFolder.Replace("\\", "/"),
            HostFolder = hostFolder.Replace("\\", "/"),
            FolderSize = GetDirectorySize(hostFolder),

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
            FontsFile = fontsFileHashMap
        };
        
        return metadata;
    }
    
    private static bool VideoIsHdr(VideoStream videoFile)
    {
        if (videoFile is null)
            throw new("Video stream is null");
        if (string.IsNullOrEmpty(videoFile.ColorSpace)) return false;
        if (videoFile.ColorSpace.Contains(ColorSpaces.Bt2020)) return true;
        return false;
    }

    private static List<IVideo> GetVideoHashList(string hostFolder, Ffprobe ffprobe)
    {
        List<IVideo> videos = [];
        
        string[] videoFolders = Directory.GetDirectories(hostFolder)
            .Where(folder => Path.GetFileName(folder).StartsWith("video")).ToArray();
        
        foreach (VideoStream videoFile in ffprobe.VideoStreams)
        {
            string tag = VideoIsHdr(videoFile) ? "HDR" : "SDR";
            string fileName = $"/{videoFile.Width}x{videoFile.Height}_{tag}/{videoFile.Width}x{videoFile.Height}_{tag}.m3u8";
            string? videoFolderPath = videoFolders.FirstOrDefault(vf => vf.Contains($"video_{videoFile.Width}x{videoFile.Height}_{tag}"));
            if(string.IsNullOrEmpty(videoFolderPath))
            {
                fileName = $"/{videoFile.Width}x{videoFile.Height}/{videoFile.Width}x{videoFile.Height}.m3u8";
                videoFolderPath = videoFolders.FirstOrDefault(vf => vf.Contains($"video_{videoFile.Width}x{videoFile.Height}"));
                if(string.IsNullOrEmpty(videoFolderPath)) continue;
            }
            
            string videoFilePath = Directory.GetFiles(videoFolderPath).First(file => file.EndsWith("m3u8"));
            
            videos.Add(new()
            {
                //TODO: Fix FileSize and BitRate
                FileName = fileName,
                FileHash = ComputeFileHash(videoFilePath),
                FileSize = GetDirectorySize(videoFilePath),
                Width = videoFile.Width,
                Height = videoFile.Height,
                Codec = videoFile.CodecName,
                BitRate = videoFile.BitRate
            });
        }
        
        return videos;
    }

    private static List<IAudio> GetAudioHashList(string hostFolder, Ffprobe ffprobe)
    {
        List<IAudio> audios = [];
        
        string[] audioFolders = Directory.GetDirectories(hostFolder)
            .Where(folder => Path.GetFileName(folder).StartsWith("audio"))
            .ToArray();
        
        foreach (AudioStream audioFile in ffprobe.AudioStreams)
        {
            string fileName =
                $"/audio_{audioFile.Language}_{audioFile.CodecName}/audio_{audioFile.Language}_{audioFile.CodecName}.m3u8";
            string? audioFolderPath = audioFolders.FirstOrDefault(vf => vf.Contains($"audio_{audioFile.Language}_{audioFile.CodecName}"));
            if (string.IsNullOrEmpty(audioFolderPath))
            {
                fileName = $"/audio_{audioFile.Language}/audio_{audioFile.Language}.m3u8";
                audioFolderPath = audioFolders.FirstOrDefault(vf => vf.Contains($"audio_{audioFile.Language}"));
            }
            if(string.IsNullOrEmpty(audioFolderPath)) continue;
            
            string audioFilePath = Directory.GetFiles(audioFolderPath)
                .First(file => file.EndsWith("m3u8"));
            
            audios.Add(new()
            {
                //:TODO: Fix FileSize and BitRate
                FileName = fileName,
                FileHash = ComputeFileHash(audioFilePath),
                FileSize = GetDirectorySize(audioFilePath),
                
                Codec = audioFile.CodecName,
                Language = audioFile.Language ?? "und",
                Channels = audioFile.Channels,
                BitRate = audioFile.BitRate,
                ChannelLayout = audioFile.ChannelLayout,
                SampleRate = audioFile.SampleRate
            });
        }
        
        return audios;
    }

    private static List<ISubtitle> GetSubtitleHashList(string hostFolder)
    {
        List<ISubtitle> subtitles = [];

        string subtitleFolder = Path.Combine(hostFolder, "subtitles");

        if (!Directory.Exists(subtitleFolder)) return subtitles;

        string[] subtitleFiles = Directory.GetFiles(subtitleFolder);
        foreach (string subtitleFile in subtitleFiles)
        {
            Regex regex = SubtitleFileRegex();
            Match match = regex.Match(subtitleFile);

            string path = Path.Combine(hostFolder, subtitleFile);

            if (match.Groups["type"].Value != "sign" && match.Groups["type"].Value != "song" &&
                match.Groups["type"].Value != "full") continue;

            if (match.Groups["ext"].Value == "sup") continue;
            if (match.Groups["ext"].Value == "vob") continue;

            subtitles.Add(new()
            {
                Language = match.Groups["lang"].Value,
                Type = match.Groups["type"].Value,
                FileName = "/" + Path.Combine("subtitles", Path.GetFileName(path)).Replace("\\", "/"),
                FileHash = ComputeFileHash(path),
                FileSize = GetFileSize(path),
                Codec = match.Groups["ext"].Value
            });
        }

        return subtitles;
    }

    private static List<IPreview> GetPreviewHashList(string hostFolder, List<IVideoTrack> extraFiles)
    {
        IEnumerable<IPreview> sprites = extraFiles.Where(file => file.Kind == "sprite")
            .Select(file => new IPreview
            {
                ImageFileName = "/" + (Path.GetFileName(file.File) ?? "").Replace("\\", "/"),
                ImageFileSize = GetFileSize(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
                ImageFileHash = ComputeFileHash(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? ""))
            });

        IEnumerable<IPreview> times = extraFiles.Where(file => file.Kind == "thumbnails")
            .Select(file => new IPreview
            {
                Width = GetImageDimensionsFromVtt(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")).Width,
                Height = GetImageDimensionsFromVtt(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")).Height,
                TimeFileName = "/" + (Path.GetFileName(file.File) ?? "").Replace("\\", "/"),
                TimeFileSize = GetFileSize(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
                TimeFileHash = ComputeFileHash(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? ""))
            });

        List<IPreview> previews = sprites.Zip(times, (sprite, time) => new IPreview
        {
            Width = time.Width,
            Height = time.Height,
            ImageFileName = sprite.ImageFileName,
            ImageFileSize = sprite.ImageFileSize,
            ImageFileHash = sprite.ImageFileHash,
            TimeFileName = time.TimeFileName,
            TimeFileSize = time.TimeFileSize,
            TimeFileHash = time.TimeFileHash
        }).ToList();
        return previews;
    }

    private static List<IFont> GetFontHashList(string hostFolder)
    {
        string fontFolder = Path.Combine(hostFolder, "fonts");

        List<IFont> fonts = [];

        if (!Directory.Exists(fontFolder)) return fonts;

        string[] fontFiles = Directory.GetFiles(fontFolder);
        foreach (string fontFile in fontFiles)
        {
            string path = Path.Combine(hostFolder, fontFile);
            fonts.Add(new()
            {
                FileName = "/" + Path.Combine("fonts", Path.GetFileName(path)).Replace("\\", "/"),
                FileHash = ComputeFileHash(path),
                FileSize = GetFileSize(path)
            });
        }

        return fonts;
    }

    private static List<IChapter> GetChapterHashList(string hostFolder, string file)
    {
        string path = Path.Combine(hostFolder, file);

        List<IChapter> chapters = [];
        
        List<IChapter>? parsedChapters = ParseChapters(path);

        foreach (IChapter parsedChapter in parsedChapters ?? [])
        {
            chapters.Add(new()
            {
                EndTime = parsedChapter.EndTime,
                StartTime = parsedChapter.StartTime,
                Title = parsedChapter.Title,
                Id = parsedChapter.Id
            });
        }

        return chapters;
    }

    private static long GetDirectorySize(string folder)
    {
        DirectoryInfo directoryInfo = new(folder);

        if (!directoryInfo.Exists) return 0;

        FileInfo[] dirs = directoryInfo.GetFiles("*", SearchOption.AllDirectories);

        long totalSize = dirs.Sum(file => file.Length);

        return totalSize;
    }

    private static long GetFileSize(string file)
    {
        FileInfo fileInfo = new(file);

        if (!fileInfo.Exists) return 0;

        return fileInfo.Length;
    }

    private static void MoveFolder(string sourceFolder, string destinationFolder)
    {
        if (Directory.Exists(sourceFolder))
        {
            Directory.Move(sourceFolder, destinationFolder);

            Logger.App($"Moved {sourceFolder} to {destinationFolder}");
        }
        else
        {
            throw new DirectoryNotFoundException($"Source folder not found: {sourceFolder}");
        }
    }

    private static (int Width, int Height) GetImageDimensions(string filePath)
    {
        SixLabors.ImageSharp.ImageInfo info = Image.Identify(filePath);

        return (info.Width, info.Height);
    }

    private static (int Width, int Height) GetImageDimensionsFromVtt(string filePath)
    {
        string vttContents = File.ReadAllText(filePath);
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
    
    private static List<IChapter>? ParseChapters(string chapterFile)
    {
        using FileStream fileStream = File.OpenRead(chapterFile);
            
        SubtitleParserResultModel? chapterParser = SubtitleParser.ParseStream(fileStream);
        
        if(chapterParser?.Subtitles == null || chapterParser.Subtitles.Count == 0) return null;

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
                Title = subtitleParserResult.Lines.First()
            };

            chapters.Add(chapter);
        }

        return chapters;
    }

    private T? GetMetaDataItem<T>(string hostFolder, string key, IEnumerable<IVideoTrack> extraFiles) where T : class
    {
        IVideoTrack? item = extraFiles.FirstOrDefault(file => file.Kind == key);
        if (item == null) return null;
        
        string path = Path.Combine(hostFolder, (Path.GetFileName(item.File) ?? "").Replace("/", ""));
        return new IHash
        {
            FileName = Path.DirectorySeparatorChar + Path.GetFileName(item.File),
            FileSize = GetFileSize(path),
            FileHash = ComputeFileHash(path)
        } as T;

    }

    private static List<IVideoTrack> GetExtraFiles(string hostFolder)
    {
        List<IVideoTrack> tracks = [];

        string[] files = Directory.GetFiles(hostFolder);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("chapter"))
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "chapters"
                });
            else if (name.StartsWith("skipper"))
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "skippers"
                });
            else if ((name.StartsWith("sprite") || name.StartsWith("preview") || name.StartsWith("thumb")) &&
                     file.EndsWith("vtt"))
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "thumbnails"
                });
            else if ((name.StartsWith("sprite") || name.StartsWith("thumb")) && file.EndsWith("webp"))
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "sprite"
                });
            else if (name.StartsWith("fonts"))
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "fonts"
                });
        }

        return tracks;
    }

    private static List<Subtitle> GetSubtitles(string hostFolder)
    {
        string subtitleFolder = Path.Combine(hostFolder, "subtitles");

        List<Subtitle> subtitles = [];

        if (!Directory.Exists(subtitleFolder)) return subtitles;
        
        string[] subtitleFiles = Directory.GetFiles(subtitleFolder);
        foreach (string subtitleFile in subtitleFiles)
        {
            Regex regex = SubtitleFileRegex();
            Match match = regex.Match(subtitleFile);

            if (match.Groups["type"].Value != "sign" && match.Groups["type"].Value != "song" &&
                match.Groups["type"].Value != "full") continue;

            if (match.Groups["ext"].Value == "sup") continue;
            if (match.Groups["ext"].Value == "vob") continue;

            subtitles.Add(new()
            {
                Language = match.Groups["lang"].Value,
                Type = match.Groups["type"].Value,
                Ext = match.Groups["ext"].Value
            });
        }

        return subtitles;
    }

    private async Task MediaType(int id, Library library)
    {
        (Movie, Show, Type) = await fileRepository.MediaType(id, library);
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> GetFiles(Library library, string path)
    {
        MediaScan mediaScan = new();

        int depth = library.Type switch
        {
            Config.MovieMediaType => 1,
            Config.TvMediaType or Config.AnimeMediaType => 2,
            _ => 0
        };

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .FilterByMediaType(library.Type)
            .FilterByFileName(Filter)
            .Process(path, depth);

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
            _ => ""
        };

        if (folder == null)
        {
            Logger.App("Folder not set");
            return folders;
        }

        using MediaContext mediaContext = new();
        Folder[] rootFolders = mediaContext.FolderLibrary
            .Select(f => f.Folder)
            .ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            string path = Path.Combine(rootFolder.Path, folder);

            if (!Directory.Exists(path))
            {
                string? match = Str.FindMatchingDirectory(rootFolder.Path, folder);
                if (match != null)
                    path = match;
            }

            if (Directory.Exists(path))
                folders.Add(new()
                {
                    Path = path,
                    Id = rootFolder.Id
                });
        }

        return folders;
    }

    private static string ComputeFileHash(string filePath)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream fileStream = File.OpenRead(filePath);

        byte[] hashBytes = sha256.ComputeHash(fileStream);

        StringBuilder hashStringBuilder = new(64);

        foreach (byte b in hashBytes) hashStringBuilder.Append(b.ToString("x2"));
        return hashStringBuilder.ToString();
    }

    [GeneratedRegex(@"(?<lang>\w{3}).(?<type>\w{3,4}).(?<ext>\w{3})$")]
    private static partial Regex SubtitleFileRegex();
    
    [GeneratedRegex(@"#xywh=\d+,\d+,(?<width>\d+),(?<height>\d+)")]
    private static partial Regex ImageDimensions();
}