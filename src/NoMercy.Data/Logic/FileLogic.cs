using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Data.Logic;

public partial class FileLogic(int id, Library library) : IDisposable, IAsyncDisposable
{
    private readonly MediaContext _mediaContext = new();

    private int Id { get; set; } = id;
    private Library Library { get; set; } = library;
    private Movie? Movie { get; set; }
    private Tv? Show { get; set; }

    private List<Folder> Folders { get; set; } = [];
    public List<MediaFolderExtend> Files { get; set; } = [];
    public string Type { get; set; } = "";

    public async Task Process()
    {
        await MediaType();
        Paths();

        foreach (Folder folder in Folders)
        {
            ConcurrentBag<MediaFolderExtend> files = await GetFiles(folder.Path);

            if (!files.IsEmpty) Files.AddRange(files);
        }

        switch (Library.Type)
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

    private async Task StoreMusic()
    {
        MediaFile? item = Files.FirstOrDefault(file => file.Parsed.Title is not null)
            ?.Files?.FirstOrDefault(file => file.Parsed is not null);

        if (item == null) return;

        await StoreAudioItem(item);
    }

    private async Task StoreMovie()
    {
        MediaFile? item = Files
            .SelectMany(file => file.Files ?? [])
            .FirstOrDefault(file => file.Parsed is not null);

        if (item == null) return;

        await StoreVideoItem(item);
    }

    private async Task StoreTvShow()
    {
        List<MediaFile> items = Files
            .SelectMany(file => file.Files ?? [])
            .Where(mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        if (items.Count == 0) return;

        foreach (MediaFile item in items) await StoreVideoItem(item);
    }

    public class Subtitle
    {
        [JsonProperty("language")] public string Language { get; set; } = string.Empty;
        [JsonProperty("type")] public string Type { get; set; } = string.Empty;
        [JsonProperty("ext")] public string Ext { get; set; } = string.Empty;
    }

    private async Task StoreAudioItem(MediaFile? item)
    {
        if (item?.Parsed is null) return;

        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null) return;

        await Task.CompletedTask;
    }

    private async Task StoreVideoItem(MediaFile? item)
    {
        if (item?.Parsed is null) return;

        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null) return;

        List<Subtitle> subtitles = [];

        string fileName = Path.DirectorySeparatorChar + Path.GetFileName(item.Path);
        string hostFolder = item.Path.Replace(fileName, "");
        string baseFolder = Path.DirectorySeparatorChar + (Movie?.Folder ?? Show?.Folder ?? "").Replace("/", "")
                                                        + item.Path.Replace(folder.Path, "")
                                                            .Replace(fileName, "");

        string subtitleFolder = Path.Combine(hostFolder, "subtitles");

        if (Directory.Exists(subtitleFolder))
        {
            string[] subtitleFiles = Directory.GetFiles(subtitleFolder);
            foreach (string subtitleFile in subtitleFiles)
            {
                Regex regex = SubtitleFileTagsRegex();
                Match match = regex.Match(subtitleFile);

                if (match.Groups["type"].Value != "sign" && match.Groups["type"].Value != "song" &&
                    match.Groups["type"].Value != "full") continue;

                subtitles.Add(new()
                {
                    Language = match.Groups["lang"].Value,
                    Type = match.Groups["type"].Value,
                    Ext = match.Groups["ext"].Value
                });
            }
        }

        Episode? episode = await _mediaContext.Episodes
            .Where(e => Show != null && e.TvId == Show.Id)
            .Where(e => e.SeasonNumber == item.Parsed.Season)
            .Where(e => e.EpisodeNumber == item.Parsed.Episode)
            .FirstOrDefaultAsync();

        try
        {
            VideoFile videoFile = new()
            {
                EpisodeId = episode?.Id,
                MovieId = Movie?.Id,
                Folder = baseFolder.Replace("\\", "/"),
                HostFolder = hostFolder.Replace("\\", "/"),
                Filename = fileName.Replace("\\", "/"),

                Share = folder.Id.ToString() ?? "",
                Duration = Regex.Replace(
                    Regex.Replace(item.FFprobe?.Duration.ToString() ?? "", "\\.\\d+", "")
                    , "^00:", ""),
                // Chapters = JsonConvert.SerializeObject(item.FFprobe?.Chapters ?? []),
                Chapters = "",
                Languages = JsonConvert.SerializeObject(item.FFprobe?.AudioStreams.Select(stream => stream.Language)
                    .Where(stream => stream != null && stream != "und")),
                Quality = item.FFprobe?.VideoStreams.FirstOrDefault()?.Width.ToString() ?? "",
                Subtitles = JsonConvert.SerializeObject(subtitles)
            };

            await _mediaContext.VideoFiles.Upsert(videoFile)
                .On(vf => vf.Filename)
                .WhenMatched((vs, vi) => new()
                {
                    Id = vi.Id,
                    EpisodeId = vi.EpisodeId,
                    MovieId = vi.MovieId,
                    Folder = vi.Folder,
                    HostFolder = vi.HostFolder,
                    Filename = vi.Filename,
                    Share = vi.Share,
                    Duration = vi.Duration,
                    Chapters = vi.Chapters,
                    Languages = vi.Languages,
                    Quality = vi.Quality,
                    Subtitles = vi.Subtitles,
                    UpdatedAt = vi.UpdatedAt
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
        }
    }

    private async Task MediaType()
    {
        switch (Library.Type)
        {
            case "movie":
                Movie = await _mediaContext.Movies
                    .Where(m => m.Id == Id)
                    .FirstOrDefaultAsync();
                Type = "movie";
                break;
            case "tv":
                Show = await _mediaContext.Tvs
                    .Where(t => t.Id == Id)
                    .FirstOrDefaultAsync();
                Type = "tv";
                break;
            case "anime":
                Show = await _mediaContext.Tvs
                    .Where(t => t.Id == Id)
                    .FirstOrDefaultAsync();
                Type = "anime";
                break;
        }
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> GetFiles(string path)
    {
        MediaScan mediaScan = new();

        int depth = Library.Type switch
        {
            "movie" => 1,
            "tv" => 2,
            "anime" => 2,
            _ => 1
        };

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType(Library.Type)
            .Process(path, depth);

        await mediaScan.DisposeAsync();

        return folders;
    }

    private void Paths()
    {
        string? folder = Library.Type switch
        {
            "movie" => Movie?.Folder?.Replace("/", ""),
            "tv" => Show?.Folder?.Replace("/", ""),
            "anime" => Show?.Folder?.Replace("/", ""),
            _ => ""
        };

        if (folder == null) return;

        Folder[] rootFolders = Library.FolderLibraries
            .Select(f => f.Folder)
            .ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            string path = Path.Combine(rootFolder.Path, folder);

            if (Directory.Exists(path))
                Folders.Add(new()
                {
                    Path = path,
                    Id = rootFolder.Id
                });
        }
    }

    [GeneratedRegex(@"(?<lang>\w{3}).(?<type>\w{3,4}).(?<ext>\w{3})$")]
    private static partial Regex SubtitleFileTagsRegex();

    public void Dispose()
    {
        _mediaContext.Dispose();
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }

    public async ValueTask DisposeAsync()
    {
        await _mediaContext.DisposeAsync();
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }
}