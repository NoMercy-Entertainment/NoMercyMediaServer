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
            .Libraries.Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(library => library.LibraryTvs)
                .ThenInclude(folder => folder.Tv)
                    .ThenInclude(tv => tv.Episodes)
                        .ThenInclude(episode => episode.VideoFiles)
                            .ThenInclude(file => file.Metadata)
            .Include(folder => folder.LibraryMovies)
                .ThenInclude(folder => folder.Movie)
                    .ThenInclude(movie => movie.VideoFiles)
                        .ThenInclude(file => file.Metadata)
            .Include(folder => folder.AlbumLibraries)
                .ThenInclude(folder => folder.Album)
                    .ThenInclude(file => file.Metadata)
            .ToListAsync();

        // Deltas are summed into a thread-safe map first (plain long addition —
        // commutative regardless of Parallel.ForEachAsync's scheduling order),
        // then applied to the shared Usage objects in a single sequential pass
        // below. storage.Data.X += ... is NOT atomic, and two libraries can
        // resolve to the same StorageDto when their folders share a path —
        // mutating it directly from inside the parallel loop lost updates.
        ConcurrentDictionary<string, StorageUsageDelta> deltas = new();

        await Parallel.ForEachAsync(
            libraries,
            SystemParallelism.Options,
            (library, _) =>
            {
                List<Metadata?> movieMetaData = library
                    .LibraryMovies.Select(l => l.Movie)
                    .SelectMany(m => m.VideoFiles)
                    .Where(m => m.Metadata is not null)
                    .Select(vf => vf.Metadata)
                    .ToList();

                List<Metadata?> tvMetaData = library
                    .LibraryTvs.Select(l => l.Tv)
                    .SelectMany(t => t.Episodes)
                    .SelectMany(e => e.VideoFiles)
                    .Where(m => m.Metadata is not null)
                    .Select(vf => vf.Metadata)
                    .ToList();

                List<Metadata?> albumMetaData = library
                    .AlbumLibraries.Select(l => l.Album)
                    .Where(m => m.Metadata is not null)
                    .Select(vf => vf.Metadata)
                    .ToList();

                foreach (FolderLibrary folderLibraries in library.FolderLibraries)
                {
                    string path = folderLibraries.Folder.Path;
                    StorageDto? storage = Storage.Find(s => s.Path == path);

                    if (storage?.Data is null)
                        return default;

                    StorageUsageDelta delta = ComputeFolderUsageDelta(
                        path,
                        movieMetaData,
                        tvMetaData,
                        albumMetaData
                    );

                    deltas.AddOrUpdate(path, delta, (_, existing) => existing.Add(delta));
                }

                return default;
            }
        );

        foreach ((string path, StorageUsageDelta delta) in deltas)
        {
            StorageDto? storage = Storage.Find(s => s.Path == path);

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
        string normalizedPath = folderPath.Replace("\\", "/");

        long moviesDelta = 0;
        long showsDelta = 0;
        long musicDelta = 0;
        long otherDelta = 0;
        long usedDelta = 0;

        if (movieMetaData.Count > 0)
            foreach (
                Metadata? metadata in movieMetaData.Where(metadata =>
                    metadata?.HostFolder.StartsWith(normalizedPath) ?? false
                )
            )
            {
                moviesDelta += metadata?.MovieSize ?? 0;
                otherDelta += metadata?.OtherSize ?? 0;
                usedDelta += metadata?.FolderSize ?? 0;
            }

        if (tvMetaData.Count > 0)
            foreach (
                Metadata? metadata in tvMetaData.Where(metadata =>
                    metadata?.HostFolder.StartsWith(normalizedPath) ?? false
                )
            )
            {
                showsDelta += metadata?.TvSize ?? 0;
                otherDelta += metadata?.OtherSize ?? 0;
                usedDelta += metadata?.FolderSize ?? 0;
            }

        if (albumMetaData.Count > 0)
            foreach (
                Metadata? metadata in albumMetaData.Where(metadata =>
                    metadata?.HostFolder.StartsWith(normalizedPath) ?? false
                )
            )
            {
                musicDelta += metadata?.MusicSize ?? 0;
                otherDelta += metadata?.OtherSize ?? 0;
                usedDelta += metadata?.FolderSize ?? 0;
            }

        return new(moviesDelta, showsDelta, musicDelta, otherDelta, usedDelta);
    }

    private static long GetDirectorySize(DirectoryInfo directoryInfo)
    {
        if (!directoryInfo.Exists)
            return 0;

        FileInfo[] dirs = directoryInfo.GetFiles("*", SearchOption.AllDirectories);

        long totalSize = dirs.Sum(file => file.Length);

        return totalSize;
    }

    private static async Task CountFolder(List<string> folders, string library, StorageDto storage)
    {
        await Parallel.ForEachAsync(
            folders,
            SystemParallelism.Options,
            (folder, _) =>
            {
                long size = GetDirectorySize(new(folder));

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
            Movies + other.Movies,
            Shows + other.Shows,
            Music + other.Music,
            Other + other.Other,
            Used + other.Used
        );
}
