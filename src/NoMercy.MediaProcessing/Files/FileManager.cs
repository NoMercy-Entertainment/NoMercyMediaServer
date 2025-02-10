using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.NmSystem;
using Serilog.Events;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.MediaProcessing.Files;
public partial class FileManager(
    IFileRepository fileRepository,
    JobDispatcher jobDispatcher
) : IFileManager
{
    private int Id { get; set; }
    private Movie? Movie { get; set; }
    private Tv? Show { get; set; }

    private List<Folder> Folders { get; set; } = [];
    private List<MediaFolderExtend> Files { get; set; } = [];
    public string Type { get; set; } = "";

    public async Task FindFiles(int id, Library library)
    {
        Id = id;

        await MediaType(id, library);
        Folders = Paths(library, Movie, Show);

        foreach (Folder folder in Folders)
        {
            Logger.App($"Scanning {Movie?.Title ?? Show?.Title} for files in {folder.Path}");

            ConcurrentBag<MediaFolderExtend> files = await GetFiles(library, folder.Path);

            if (!files.IsEmpty) Files.AddRange(files);
        }

        switch (library.Type)
        {
            case "movie":
                await StoreMovie();
                break;
            case "tv":
            case "anime":
                await StoreTvShow();
                break;
            case "music":
                await StoreMusic();
                break;
            default:
                Logger.App("Unknown library type");
                break;
        }
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
        {
            foreach (FolderLibrary libraryFolder in tv.Library.FolderLibraries)
            {
                string path = libraryFolder.Folder.Path + tv.Folder;
                if (!Directory.Exists(path)) continue;
                
                folderName = tv.Folder;
                sourceFolder = path;
                
                break;
            }
        }
        else if (movie?.Folder is not null)
        {
            foreach (FolderLibrary libraryFolder in movie.Library.FolderLibraries)
            {
                string path = libraryFolder.Folder.Path + movie.Folder;
                if (!Directory.Exists(path)) continue;
                
                folderName = movie.Folder;
                sourceFolder = path;
                
                break;
            }
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

            if (libraryTv is not null)
            {
                libraryTv.LibraryId = newFolderLibrary.LibraryId;
            }

            await context.SaveChangesAsync();
        }
        else if (movie?.Folder is not null)
        {
            movie.Folder = folderName;
            movie.LibraryId = newFolderLibrary.LibraryId;
            
            LibraryMovie? libraryMovie = await context.LibraryMovie
                .FirstOrDefaultAsync(lm => lm.MovieId == movie.Id);

            if (libraryMovie is not null)
            {
                libraryMovie.LibraryId = newFolderLibrary.LibraryId;
            }

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

        foreach (MediaFile item in items) await StoreVideoItem(item);

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

        string fileName = Path.DirectorySeparatorChar + Path.GetFileName(item.Path);
        string hostFolder = item.Path.Replace(fileName, "");
        string baseFolder = (Path.DirectorySeparatorChar + (Movie?.Folder ?? Show?.Folder ?? "").Replace("/", "")
                                                         + item.Path.Replace(folder.Path, ""))
                                                            .Replace(fileName, "");

        List<Subtitle> subtitles = GetSubtitles(hostFolder);

        List<IVideoTrack> tracks = GetExtraFiles(hostFolder);

        Episode? episode = await fileRepository.GetEpisode(Show?.Id, item);

        Metadata metadata = MakeMetadata(item, fileName, baseFolder, hostFolder, tracks);
        
        Ulid metadataId = await fileRepository.StoreMetadata(metadata);

        try
        {
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
                MetadataId = metadataId,
            };

            await fileRepository.StoreVideoFile(videoFile);

            await fileRepository.SetCreatedAt(videoFile);
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
        }
    }

    private Metadata MakeMetadata(MediaFile item, string fileName, string baseFolder, string hostFolder, List<IVideoTrack> extraFiles)
    {
        List<IVideo> video = GetVideoHashList(hostFolder);
        List<IAudio> audio = GetAudioHashList(hostFolder);
        List<ISubtitle> subtitles = GetSubtitleHashList(hostFolder);
        List<IFont> fonts = GetFontHashList(hostFolder);
        List<IPreview> previews = GetPreviewHashList(hostFolder, extraFiles);
        
        IChaptersFile? chapters = GetMetaDataItem<IChaptersFile>(hostFolder, "chapters", extraFiles);
        
        IFontsFile? fontsFile = extraFiles.Where(file => file.Kind == "fonts")
            .Select(file => new IFontsFile
            {
                FileName = (Path.GetFileName(file.File) ?? "").Replace("\\", "/"),
                FileSize = GetFileSize(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
                FileHash = ComputeFileHash(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
            }).FirstOrDefault();
        
        Metadata metadata = new()
        {
            Filename = fileName.Replace("\\", "/"),
            Duration = item.FFprobe?.Duration.ToString() ?? "",
            Folder = baseFolder.Replace("\\", "/"),
            HostFolder = hostFolder.Replace("\\", "/"),
            FolderSize = GetDirectorySize(hostFolder),
            
            Type = Movie?.Id is not null 
                ? Database.Models.MediaType.Movie 
                : Database.Models.MediaType.Tv,
            
            Audio = audio,
            Fonts = fonts,
            Previews = previews,
            Subtitles = subtitles,
            Video = video,
            Chapters = chapters,
            FontsFile = fontsFile,
        };
        return metadata;
    }

    private static List<IVideo> GetVideoHashList(string hostFolder)
    {
        List<IVideo> videos = [];

        string[] videoFolders = Directory.GetDirectories(hostFolder).Where(folder => Path.GetFileName(folder).StartsWith("video")).ToArray();

        foreach (string videoFolder in videoFolders)
        {
            string path = Path.Combine(hostFolder, videoFolder);
            
            string[] videoFiles = Directory.GetFiles(path).Where(file => file.EndsWith("m3u8")).ToArray();
            foreach (string videoFile in videoFiles)
            {
                try
                {
                    IMediaAnalysis ffprobe = FFProbe.Analyse(videoFile);
                    videos.Add(new()
                    {
                        FileName = Path.Combine(Path.GetFileName(path), Path.GetFileName(videoFile)).Replace("\\", "/"),
                        FileHash = ComputeFileHash(videoFile),
                        FileSize = GetDirectorySize(path),
                        Width = ffprobe.VideoStreams.FirstOrDefault()?.Width ?? 0,
                        Height = ffprobe.VideoStreams.FirstOrDefault()?.Height ?? 0,
                        Codec = ffprobe.AudioStreams.FirstOrDefault()?.CodecName ?? "",
                        BitRate = ffprobe.AudioStreams.FirstOrDefault()?.BitRate ?? 0,
                    });
                }
                catch (Exception)
                {
                    //
                }
            }
        }
        
        return videos;
    }
    
    private static List<IAudio> GetAudioHashList(string hostFolder)
    {
        List<IAudio> audios = [];
        
        string[] audioFolders = Directory.GetDirectories(hostFolder).Where(folder => Path.GetFileName(folder).StartsWith("audio")).ToArray();
        foreach (string audioFolder in audioFolders)
        {
            string path = Path.Combine(hostFolder, audioFolder);
            
            string[] audioFiles = Directory.GetFiles(path).Where(file => file.EndsWith("m3u8")).ToArray();
            foreach (string audioFile in audioFiles)
            {
                try
                {
                    IMediaAnalysis ffprobe = FFProbe.Analyse(audioFile);
                
                    audios.Add(new()
                    {
                        FileName = Path.Combine(Path.GetFileName(path), Path.GetFileName(audioFile)).Replace("\\", "/"),
                        FileHash = ComputeFileHash(audioFile),
                        FileSize = GetDirectorySize(path),
                        Codec = ffprobe.AudioStreams.FirstOrDefault()?.CodecName ?? "",
                        Language = audioFile.Split("_")[1].Split("\\")[0],
                        Channels = ffprobe.AudioStreams.FirstOrDefault()?.Channels ?? 0,
                        BitRate = ffprobe.AudioStreams.FirstOrDefault()?.BitRate ?? 0,
                        ChannelLayout = ffprobe.AudioStreams.FirstOrDefault()?.ChannelLayout ?? "",
                        SampleRate = ffprobe.AudioStreams.FirstOrDefault()?.SampleRateHz ?? 0,
                    });
                }
                catch (Exception)
                {
                   //
                }
            }
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

            if(match.Groups["ext"].Value == "sup") continue;

            subtitles.Add(new()
            {
                Language = match.Groups["lang"].Value,
                Type = match.Groups["type"].Value,
                FileName = Path.Combine("subtitles", Path.GetFileName(path)).Replace("\\", "/"),
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
                ImageFileName = (Path.GetFileName(file.File) ?? "").Replace("\\", "/"),
                ImageFileSize = GetFileSize(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
                ImageFileHash = ComputeFileHash(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),

            });
        
        IEnumerable<IPreview> times = extraFiles.Where(file => file.Kind == "thumbnails")
            .Select(file => new IPreview
            {
                Width = GetImageDimensionsFromVtt(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")).Width,
                Height = GetImageDimensionsFromVtt(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")).Height,
                TimeFileName = (Path.GetFileName(file.File) ?? "").Replace("\\", "/"),
                TimeFileSize = GetFileSize(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
                TimeFileHash = ComputeFileHash(Path.Combine(hostFolder, Path.GetFileName(file.File) ?? "")),
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
                FileName = Path.Combine("fonts", Path.GetFileName(path)).Replace("\\", "/"),
                FileHash = ComputeFileHash(path),
                FileSize = GetFileSize(path)
            });
        }

        return fonts;
    }

    private static long GetDirectorySize(string folder)
    {
        DirectoryInfo directoryInfo = new(folder);
        
        if (!directoryInfo.Exists)
        {
            return 0;
        }

        FileInfo[] dirs = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
            
        long totalSize = dirs.Sum(file => file.Length);

        return totalSize;
    }
    
    private static long GetFileSize(string file)
    {
        FileInfo fileInfo = new(file);
        
        if (!fileInfo.Exists)
        {
            return 0;
        }

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
        Image image = Image.Load(filePath);
        
        return (image.Width, image.Height);
    }

    private static (int Width, int Height) GetImageDimensionsFromVtt(string filePath)
    {
        string vttContents = File.ReadAllText(filePath);
        Regex regex = new(@"#xywh=\d+,\d+,(?<width>\d+),(?<height>\d+)");
        Match match = regex.Match(vttContents);

        if (match.Success)
        {
            int width = int.Parse(match.Groups["width"].Value);
            int height = int.Parse(match.Groups["height"].Value);
            return (width, height);
        }
        
        return (0, 0);
    }
    
    private T? GetMetaDataItem<T>(string hostFolder, string key, IEnumerable<IVideoTrack> extraFiles) where T : class
    {
        IVideoTrack? item = extraFiles.FirstOrDefault(file => file.Kind == key);
        if (item != null)
        {
            string path = Path.Combine(hostFolder, (Path.GetFileName(item.File) ?? "").Replace("/", ""));
            return new IHash()
            {
                FileName =  Path.DirectorySeparatorChar + Path.GetFileName(item.File),
                FileSize = GetFileSize(path),
                FileHash = ComputeFileHash(path)
            } as T;
        }

        return null;
    }

    private static List<IVideoTrack> GetExtraFiles(string hostFolder)
    {
        List<IVideoTrack> tracks = [];
        
        string[] files = Directory.GetFiles(hostFolder);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("chapter"))
            {
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "chapters"
                });
            }
            else if (name.StartsWith("skipper"))
            {
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "skippers"
                });
            }
            else if ((name.StartsWith("sprite") || name.StartsWith("preview") || name.StartsWith("thumb")) && file.EndsWith("vtt"))
            {
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "thumbnails"
                });
            }
            else if ((name.StartsWith("sprite") || name.StartsWith("thumb")) && file.EndsWith("webp"))
            {
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "sprite"
                });
            }
            else if (name.StartsWith("fonts"))
            {
                tracks.Add(new()
                {
                    File = "/" + name,
                    Kind = "fonts"
                });
            }
        }

        return tracks;
    }

    private static List<Subtitle> GetSubtitles(string hostFolder)
    {
        string subtitleFolder = Path.Combine(hostFolder, "subtitles");

        List<Subtitle> subtitles = [];

        if (Directory.Exists(subtitleFolder))
        {
            string[] subtitleFiles = Directory.GetFiles(subtitleFolder);
            foreach (string subtitleFile in subtitleFiles)
            {
                Regex regex = SubtitleFileRegex();
                Match match = regex.Match(subtitleFile);

                if (match.Groups["type"].Value != "sign" && match.Groups["type"].Value != "song" &&
                    match.Groups["type"].Value != "full") continue;

                if(match.Groups["ext"].Value == "sup") continue;

                subtitles.Add(new()
                {
                    Language = match.Groups["lang"].Value,
                    Type = match.Groups["type"].Value,
                    Ext = match.Groups["ext"].Value
                });
            }
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
            "movie" => 1,
            "tv" or "anime" => 2,
            _ => 0
        };

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType(library.Type)
            .Process(path, depth);

        await mediaScan.DisposeAsync();

        return folders;
    }

    private List<Folder> Paths(Library library, Movie? movie = null, Tv? show = null)
    {
        List<Folder> folders = new();
        string? folder = library.Type switch
        {
            "movie" => movie?.Folder?.Replace("/", ""),
            "tv" or "anime" => show?.Folder?.Replace("/", ""),
            _ => ""
        };

        if (folder == null) return folders;

        Folder[] rootFolders = library.FolderLibraries
            .Select(f => f.Folder)
            .ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            string path = Path.Combine(rootFolder.Path, folder);

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
        
        foreach (byte b in hashBytes)
        {
            hashStringBuilder.Append(b.ToString("x2"));
        }
        return hashStringBuilder.ToString();
    }

    [GeneratedRegex(@"(?<lang>\w{3}).(?<type>\w{3,4}).(?<ext>\w{3})$")]
    private static partial Regex SubtitleFileRegex();
}