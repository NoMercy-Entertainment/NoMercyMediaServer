using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Data.Logic;

public partial class FileLogic(
    int id,
    Library library,
    MediaContext mediaContext,
    IStorageFactory storageFactory,
    IStorageDriver storageDriver
) : IDisposable, IAsyncDisposable
{
    private readonly MediaContext _mediaContext = mediaContext;
    private readonly IStorageFactory _storageFactory = storageFactory;
    private readonly IStorageDriver _storageDriver = storageDriver;

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
            ConcurrentBag<MediaFolderExtend> files = await GetFiles(folder);

            if (!files.IsEmpty)
                Files.AddRange(files);
        }

        switch (Library.Type)
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
    }

    private async Task StoreMusic()
    {
        MediaFile? item = Files
            .FirstOrDefault(file => file.Parsed.Title is not null)
            ?.Files?.FirstOrDefault(file => file.Parsed is not null);

        if (item == null)
            return;

        await StoreAudioItem(item);
    }

    private async Task StoreMovie()
    {
        MediaFile? item = Files
            .SelectMany(file => file.Files ?? [])
            .FirstOrDefault(file => file.Parsed is not null);

        if (item == null)
            return;

        await StoreVideoItem(item);
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
            await StoreVideoItem(item);
    }

    public class Subtitle
    {
        [JsonProperty("language")]
        public string Language { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("ext")]
        public string Ext { get; set; } = string.Empty;
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

    private async Task StoreVideoItem(MediaFile? item)
    {
        if (item?.Parsed is null)
            return;

        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null)
            return;

        List<Subtitle> subtitles = [];

        string itemPath = item.Path.Replace('\\', '/');
        string fileName = "/" + StoragePathHelpers.GetName(itemPath);
        string hostFolder = itemPath.Replace(fileName, "");
        string showName = (Movie?.Folder ?? Show?.Folder).OrEmpty().Trim('/', '\\');
        int showIdx = string.IsNullOrEmpty(showName)
            ? -1
            : itemPath.IndexOf(showName, StringComparison.OrdinalIgnoreCase);
        string baseFolder =
            showIdx >= 0 ? ("/" + itemPath[showIdx..]).Replace(fileName, "") : hostFolder;

        string subtitleFolder = hostFolder.TrimEnd('/') + "/subtitles";

        IStorage storage = _storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        if (await storage.ExistsAsync(subtitleFolder, CancellationToken.None))
        {
            IReadOnlyList<StorageEntry> subtitleEntries = storage.List(subtitleFolder, "*", false);
            foreach (string subtitleFile in subtitleEntries.Select(e => e.Path))
            {
                Regex regex = SubtitleFileTagsRegex();
                Match match = regex.Match(subtitleFile);

                if (!match.Success)
                    continue;

                // Reject binary subtitle formats we can't stream as HLS sidecars;
                // accept every text format (vtt, ass, srt, ssa, sub, idx, webvtt)
                // and every variant (sign, full, sdh, alt, ...).
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
        }

        Episode? episode = await _mediaContext
            .Episodes.Where(e => Show != null && e.TvId == Show.Id)
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
            };

            await _mediaContext
                .VideoFiles.Upsert(videoFile)
                .On(vf => vf.Filename)
                .WhenMatched(
                    (vs, vi) =>
                        new()
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
                        }
                )
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
            case Config.MovieMediaType:
                Movie = await _mediaContext.Movies.Where(m => m.Id == Id).FirstOrDefaultAsync();
                Type = Config.MovieMediaType;
                break;
            case Config.TvMediaType:
                Show = await _mediaContext.Tvs.Where(t => t.Id == Id).FirstOrDefaultAsync();
                Type = Config.TvMediaType;
                break;
            case Config.AnimeMediaType:
                Show = await _mediaContext.Tvs.Where(t => t.Id == Id).FirstOrDefaultAsync();
                Type = Config.AnimeMediaType;
                break;
        }
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> GetFiles(Folder folder)
    {
        // Resolve the per-folder driver so NFS/SMB folders use the right backend.
        IStorage folderStorage = _storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        MediaScan mediaScan = new(folderStorage.Driver);

        int depth = Library.Type switch
        {
            Config.MovieMediaType => 1,
            Config.TvMediaType => 2,
            Config.AnimeMediaType => 2,
            _ => 1,
        };

        string scanRoot = folderStorage.GetFullPath(folder.Path);

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType(Library.Type)
            .Process(scanRoot, depth);

        await mediaScan.DisposeAsync();

        return folders;
    }

    private void Paths()
    {
        string? folder = Library.Type switch
        {
            Config.MovieMediaType => Movie?.Folder?.Replace("/", ""),
            Config.TvMediaType => Show?.Folder?.Replace("/", ""),
            Config.AnimeMediaType => Show?.Folder?.Replace("/", ""),
            _ => "",
        };

        if (folder == null)
            return;

        Folder[] rootFolders = Library.FolderLibraries.Select(f => f.Folder).ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            IStorage folderStorage = _storageFactory.For(
                rootFolder.Id,
                rootFolder.DriverId,
                string.Empty
            );
            string resolvedRoot = folderStorage.GetFullPath(rootFolder.Path);
            string path = folderStorage.CombinePath(resolvedRoot, folder);

            if (!folderStorage.Exists(path))
            {
                string? match = Str.FindMatchingDirectory(_storageDriver, resolvedRoot, folder);
                if (match != null)
                    path = match;
            }

            if (folderStorage.Exists(path))
                Folders.Add(
                    new()
                    {
                        Path = path,
                        Id = rootFolder.Id,
                        DriverId = rootFolder.DriverId,
                    }
                );
        }
    }

    // Match the encoder's subtitle filename scheme: {lang}.{variant}.{ext}
    // anywhere in the filename tail. 2-3 char lang (ISO 639-1/2), any-length
    // variant (sign, full, sdh, alt, forced, …), 3-6 char extension (vtt,
    // ass, srt, ssa, sub, idx, webvtt).
    [GeneratedRegex(@"(?<lang>[a-zA-Z]{2,3})\.(?<type>\w+)\.(?<ext>\w{3,6})$")]
    private static partial Regex SubtitleFileTagsRegex();

    public void Dispose()
    {
        _mediaContext.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _mediaContext.DisposeAsync();
    }
}
