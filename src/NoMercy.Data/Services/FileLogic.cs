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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Subtitles;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;

namespace NoMercy.Data.Services;

public partial class FileLogic(
    int id,
    Library library,
    MediaContext mediaContext,
    IStorageFactory storageFactory,
    ILogger<FileLogic> logger
) : IDisposable, IAsyncDisposable
{
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
            ConcurrentBag<MediaFolderExtend> files = await GetFiles(folder: folder);

            if (!files.IsEmpty)
                Files.AddRange(collection: files);
        }

        switch (Library.Type)
        {
            case MediaTypes.MovieMediaType:
                await StoreMovie();
                break;
            case MediaTypes.TvMediaType:
            case MediaTypes.AnimeMediaType:
                await StoreTvShow();
                break;
            case MediaTypes.MusicMediaType:
                await StoreMusic();
                break;
            default:
                logger.LogInformation(message: "Unknown library type");
                break;
        }
    }

    private async Task StoreMusic()
    {
        MediaFile? item = Files
            .FirstOrDefault(predicate: file => file.Parsed.Title is not null)
            ?.Files?.FirstOrDefault(predicate: file => file.Parsed is not null);

        if (item == null)
            return;

        await StoreAudioItem(item: item);
    }

    private async Task StoreMovie()
    {
        MediaFile? item = Files
            .SelectMany(selector: file => file.Files ?? [])
            .FirstOrDefault(predicate: file => file.Parsed is not null);

        if (item == null)
            return;

        await StoreVideoItem(item: item);
    }

    private async Task StoreTvShow()
    {
        List<MediaFile> items = Files
            .SelectMany(selector: file => file.Files ?? [])
            .Where(predicate: mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        if (items.Count == 0)
            return;

        foreach (MediaFile item in items)
            await StoreVideoItem(item: item);
    }

    public class Subtitle
    {
        [JsonProperty(propertyName: "language")]
        public string Language { get; set; } = string.Empty;

        [JsonProperty(propertyName: "type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty(propertyName: "ext")]
        public string Ext { get; set; } = string.Empty;
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

    private async Task StoreVideoItem(MediaFile? item)
    {
        if (item?.Parsed is null)
            return;

        Folder? folder = Folders.FirstOrDefault(predicate: folder => item.Path.Contains(value: folder.Path));
        if (folder == null)
            return;

        List<Subtitle> subtitles = [];

        // MediaScan resolves paths through the driver, so item.Path is
        // driver-absolute. Rebase onto the scope-relative folder root so the
        // facade calls below (ExistsAsync / List) accept it on remote backends
        // and the persisted path stays backend-neutral.
        string itemPath = StoragePathHelpers.RebaseToFolderRoot(
            absolutePath: item.Path.Replace(oldChar: '\\', newChar: '/'),
            folderPath: folder.Path
        );
        string fileName = "/" + StoragePathHelpers.GetName(path: itemPath);
        string hostFolder = itemPath.Replace(oldValue: fileName, newValue: "");
        string showName = (Movie?.Folder ?? Show?.Folder).OrEmpty().Trim(trimChars: ['/', '\\']);
        int showIdx = string.IsNullOrEmpty(value: showName)
            ? -1
            : itemPath.IndexOf(value: showName, comparisonType: StringComparison.OrdinalIgnoreCase);
        string baseFolder =
            showIdx >= 0 ? ("/" + itemPath[showIdx..]).Replace(oldValue: fileName, newValue: "") : hostFolder;

        string subtitleFolder = hostFolder.TrimEnd(trimChar: '/') + "/subtitles";

        IStorage storage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
        if (await storage.ExistsAsync(path: subtitleFolder, ct: CancellationToken.None))
        {
            IReadOnlyList<StorageEntry> subtitleEntries = storage.List(path: subtitleFolder, pattern: "*", recursive: false);
            foreach (string subtitleFile in subtitleEntries.Select(selector: e => e.Path))
            {
                Regex regex = SubtitleFileTagsRegex();
                Match match = regex.Match(input: subtitleFile);

                if (!match.Success)
                    continue;

                // Reject binary subtitle formats we can't stream as HLS sidecars;
                // accept every text format (vtt, ass, srt, ssa, sub, webvtt) and
                // every variant (sign, full, sdh, alt, ...). The bitmap track's
                // own OCR sidecar carries the same {lang}.{type}, so dropping it
                // here loses nothing: the readable .vtt is listed in its place.
                string ext = match.Groups[groupname: "ext"].Value;
                if (SubtitleClassifier.IsBitmapSidecarExtension(extension: ext))
                    continue;

                subtitles.Add(
                    item: new()
                    {
                        Language = match.Groups[groupname: "lang"].Value,
                        Type = match.Groups[groupname: "type"].Value,
                        Ext = ext,
                    }
                );
            }
        }

        Episode? episode = await mediaContext
            .Episodes.Where(predicate: e => Show != null && e.TvId == Show.Id)
            .Where(predicate: e => e.SeasonNumber == item.Parsed.Season)
            .Where(predicate: e => e.EpisodeNumber == item.Parsed.Episode)
            .FirstOrDefaultAsync();

        try
        {
            VideoFile videoFile = new()
            {
                EpisodeId = episode?.Id,
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
                Languages = JsonConvert.SerializeObject(
                    value: item.FFprobe?.AudioStreams.Select(selector: stream => stream.Language)
                        .Where(predicate: stream => stream != null && stream != "und")
                ),
                Quality = (item.FFprobe?.VideoStreams.FirstOrDefault()?.Width.ToString()).OrEmpty(),
                Subtitles = JsonConvert.SerializeObject(value: subtitles),
            };

            await mediaContext
                .VideoFiles.Upsert(entity: videoFile)
                .On(match: vf => vf.Filename)
                .WhenMatched(
                    updater: (vs, vi) =>
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
            logger.LogError(message: e.Message);
        }
    }

    private async Task MediaType()
    {
        switch (Library.Type)
        {
            case MediaTypes.MovieMediaType:
                Movie = await mediaContext.Movies.Where(predicate: m => m.Id == Id).FirstOrDefaultAsync();
                Type = MediaTypes.MovieMediaType;
                break;
            case MediaTypes.TvMediaType:
                Show = await mediaContext.Tvs.Where(predicate: t => t.Id == Id).FirstOrDefaultAsync();
                Type = MediaTypes.TvMediaType;
                break;
            case MediaTypes.AnimeMediaType:
                Show = await mediaContext.Tvs.Where(predicate: t => t.Id == Id).FirstOrDefaultAsync();
                Type = MediaTypes.AnimeMediaType;
                break;
        }
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> GetFiles(Folder folder)
    {
        // Resolve the per-folder driver so NFS/SMB folders use the right backend.
        IStorage folderStorage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
        MediaScan mediaScan = new(driver: folderStorage.Driver);

        int depth = Library.Type switch
        {
            MediaTypes.MovieMediaType => 1,
            MediaTypes.TvMediaType => 2,
            MediaTypes.AnimeMediaType => 2,
            _ => 1,
        };

        // Resolve through the driver, not the IStorage facade: the facade's
        // GetFullPath is a LocalStorage-only escape hatch that throws on every
        // remote backend, so a facade call here killed every rescan of an
        // NFS / SMB / S3 / WebDAV library. The driver resolves the path within
        // its own backend, exactly as MediaScan.Process does internally.
        string scanRoot = folderStorage.Driver.GetFullPath(path: folder.Path);

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType(mediaType: Library.Type)
            .Process(rootFolder: scanRoot, depth: depth);

        await mediaScan.DisposeAsync();

        return folders;
    }

    private void Paths()
    {
        string? folder = Library.Type switch
        {
            MediaTypes.MovieMediaType => Movie?.Folder?.Replace(oldValue: "/", newValue: ""),
            MediaTypes.TvMediaType => Show?.Folder?.Replace(oldValue: "/", newValue: ""),
            MediaTypes.AnimeMediaType => Show?.Folder?.Replace(oldValue: "/", newValue: ""),
            _ => "",
        };

        if (folder == null)
            return;

        Folder[] rootFolders = Library.FolderLibraries.Select(selector: f => f.Folder).ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            IStorage folderStorage = storageFactory.For(
                folderId: rootFolder.Id,
                driverId: rootFolder.DriverId,
                subPath: string.Empty
            );
            // Stay in scope-relative space: rootFolder.Path is the storage key
            // the facade wants (e.g. "Marvels/TV.Shows"); the facade Exists / List
            // reject absolute paths. Do NOT resolve to an OS/mount-absolute path
            // here — GetFullPath produces "/mnt/vault/..." which StoragePathGuard
            // then rejects as a scope-relative key.
            string path = folderStorage.CombinePath(parent: rootFolder.Path, child: folder);

            if (!folderStorage.Exists(path: path))
            {
                // FindMatchingDirectory walks the raw driver, so it needs the
                // driver-absolute root and returns a driver-absolute directory.
                // Convert that hit back to a scope-relative key before storing.
                string resolvedRoot = folderStorage.Driver.GetFullPath(path: rootFolder.Path);
                string? match = FileNameSanitizer.FindMatchingDirectory(
                    driver: folderStorage.Driver,
                    rootPath: resolvedRoot,
                    expectedFolderName: folder
                );
                if (match != null)
                    path = folderStorage.CombinePath(parent: rootFolder.Path, child: folderStorage.GetName(path: match));
            }

            if (folderStorage.Exists(path: path))
                Folders.Add(
                    item: new()
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
    [GeneratedRegex(pattern: @"(?<lang>[a-zA-Z]{2,3})\.(?<type>\w+)\.(?<ext>\w{3,6})$")]
    private static partial Regex SubtitleFileTagsRegex();

    public void Dispose()
    {
        mediaContext.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await mediaContext.DisposeAsync();
    }
}
