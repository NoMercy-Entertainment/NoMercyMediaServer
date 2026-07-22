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
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Monitoring;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Domain;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Data.Jobs;

[Serializable]
public class StorageJob : IShouldQueue
{
    public string QueueName => "extras";
    public int Priority => 1000;

    public List<StorageDto> Storage { get; set; } = [];

    public StorageJob()
    {
        //
    }

    public StorageJob(List<StorageDto> storage)
    {
        Storage = storage;
    }

    public async Task Handle()
    {
        await using MediaContext context = new();

        List<Library> libraries = await context
            .Libraries.Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .Include(navigationPropertyPath: library => library.LibraryTvs)
                .ThenInclude(navigationPropertyPath: folder => folder.Tv)
                    .ThenInclude(navigationPropertyPath: tv => tv.Episodes)
                        .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
                            .ThenInclude(navigationPropertyPath: file => file.Metadata)
            .Include(navigationPropertyPath: folder => folder.LibraryMovies)
                .ThenInclude(navigationPropertyPath: folder => folder.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.VideoFiles)
                        .ThenInclude(navigationPropertyPath: file => file.Metadata)
            .Include(navigationPropertyPath: folder => folder.AlbumLibraries)
                .ThenInclude(navigationPropertyPath: folder => folder.Album)
                    .ThenInclude(navigationPropertyPath: file => file.Metadata)
            .ToListAsync();

        // Deltas are summed into a thread-safe map first (plain long addition —
        // commutative regardless of Parallel.ForEachAsync's scheduling order),
        // then applied to the shared Usage objects in a single sequential pass
        // below. storage.Data.X += ... is NOT atomic, and two libraries can
        // resolve to the same StorageDto when their folders share a path —
        // mutating it directly from inside the parallel loop lost updates.
        ConcurrentDictionary<string, StorageUsageDelta> deltas = new();

        await Parallel.ForEachAsync(
            source: libraries,
            parallelOptions: SystemParallelism.Options,
            body: (library, _) =>
            {
                List<Metadata?> movieMetaData = library
                    .LibraryMovies.Select(selector: l => l.Movie)
                    .SelectMany(selector: m => m.VideoFiles)
                    .Where(predicate: m => m.Metadata is not null)
                    .Select(selector: vf => vf.Metadata)
                    .ToList();

                List<Metadata?> tvMetaData = library
                    .LibraryTvs.Select(selector: l => l.Tv)
                    .SelectMany(selector: t => t.Episodes)
                    .SelectMany(selector: e => e.VideoFiles)
                    .Where(predicate: m => m.Metadata is not null)
                    .Select(selector: vf => vf.Metadata)
                    .ToList();

                List<Metadata?> albumMetaData = library
                    .AlbumLibraries.Select(selector: l => l.Album)
                    .Where(predicate: m => m.Metadata is not null)
                    .Select(selector: vf => vf.Metadata)
                    .ToList();

                foreach (FolderLibrary folderLibraries in library.FolderLibraries)
                {
                    string path = folderLibraries.Folder.Path;
                    StorageDto? storage = Storage.Find(match: s => s.Path == path);

                    if (storage?.Data is null)
                        return default;

                    StorageUsageDelta delta = ComputeFolderUsageDelta(
                        folderPath: path,
                        movieMetaData: movieMetaData,
                        tvMetaData: tvMetaData,
                        albumMetaData: albumMetaData
                    );

                    deltas.AddOrUpdate(key: path, addValue: delta, updateValueFactory: (_, existing) => existing.Add(other: delta));
                }

                return default;
            }
        );

        foreach ((string path, StorageUsageDelta delta) in deltas)
        {
            StorageDto? storage = Storage.Find(match: s => s.Path == path);

            if (storage?.Data is null)
                continue;

            storage.Data.Movies += delta.Movies;
            storage.Data.Shows += delta.Shows;
            storage.Data.Music += delta.Music;
            storage.Data.Other += delta.Other;
            storage.Data.Used += delta.Used;
        }
    }

    /// <summary>
    /// Sums the movie/tv/album metadata matching <paramref name="folderPath"/>
    /// into a single delta. Pure and allocation-light so it can run inside the
    /// parallel per-library loop without touching shared state.
    /// </summary>
    internal static StorageUsageDelta ComputeFolderUsageDelta(
        string folderPath,
        List<Metadata?> movieMetaData,
        List<Metadata?> tvMetaData,
        List<Metadata?> albumMetaData
    )
    {
        string normalizedPath = folderPath.Replace(oldValue: "\\", newValue: "/");

        long moviesDelta = 0;
        long showsDelta = 0;
        long musicDelta = 0;
        long otherDelta = 0;
        long usedDelta = 0;

        if (movieMetaData.Count > 0)
            foreach (
                Metadata? metadata in movieMetaData.Where(predicate: metadata =>
                    metadata?.HostFolder.StartsWith(value: normalizedPath) ?? false
                )
            )
            {
                moviesDelta += metadata?.MovieSize ?? 0;
                otherDelta += metadata?.OtherSize ?? 0;
                usedDelta += metadata?.FolderSize ?? 0;
            }

        if (tvMetaData.Count > 0)
            foreach (
                Metadata? metadata in tvMetaData.Where(predicate: metadata =>
                    metadata?.HostFolder.StartsWith(value: normalizedPath) ?? false
                )
            )
            {
                showsDelta += metadata?.TvSize ?? 0;
                otherDelta += metadata?.OtherSize ?? 0;
                usedDelta += metadata?.FolderSize ?? 0;
            }

        if (albumMetaData.Count > 0)
            foreach (
                Metadata? metadata in albumMetaData.Where(predicate: metadata =>
                    metadata?.HostFolder.StartsWith(value: normalizedPath) ?? false
                )
            )
            {
                musicDelta += metadata?.MusicSize ?? 0;
                otherDelta += metadata?.OtherSize ?? 0;
                usedDelta += metadata?.FolderSize ?? 0;
            }

        return new(Movies: moviesDelta, Shows: showsDelta, Music: musicDelta, Other: otherDelta, Used: usedDelta);
    }

    private static long GetDirectorySize(DirectoryInfo directoryInfo)
    {
        if (!directoryInfo.Exists)
            return 0;

        FileInfo[] dirs = directoryInfo.GetFiles(searchPattern: "*", searchOption: SearchOption.AllDirectories);

        long totalSize = dirs.Sum(selector: file => file.Length);

        return totalSize;
    }

    private static async Task CountFolder(List<string> folders, string library, StorageDto storage)
    {
        await Parallel.ForEachAsync(
            source: folders,
            parallelOptions: SystemParallelism.Options,
            body: (folder, _) =>
            {
                long size = GetDirectorySize(directoryInfo: new(path: folder));

                switch (library)
                {
                    case MediaTypes.MovieMediaType:
                        storage.Data.Movies += size;
                        break;
                    case MediaTypes.TvMediaType:
                    case MediaTypes.AnimeMediaType:
                        storage.Data.Shows += size;
                        break;
                    case MediaTypes.MusicMediaType:
                        storage.Data.Music += size;
                        break;
                }

                return default;
            }
        );
    }
}

/// <summary>
/// Accumulated Movies/Shows/Music/Other/Used contribution for one folder path.
/// Summed with plain <see langword="long"/> addition — commutative and
/// associative, so merging it across threads via
/// <see cref="ConcurrentDictionary{TKey,TValue}.AddOrUpdate(TKey,TValue,System.Func{TKey,TValue,TValue})"/>
/// never loses an update regardless of task scheduling order.
/// </summary>
internal readonly record struct StorageUsageDelta(
    long Movies,
    long Shows,
    long Music,
    long Other,
    long Used
)
{
    public StorageUsageDelta Add(StorageUsageDelta other) =>
        new(
            Movies: Movies + other.Movies,
            Shows: Shows + other.Shows,
            Music: Music + other.Music,
            Other: Other + other.Other,
            Used: Used + other.Used
        );
}
