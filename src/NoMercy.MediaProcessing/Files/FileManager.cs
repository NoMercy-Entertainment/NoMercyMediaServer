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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Analysis;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager(
    IFileRepository fileRepository,
    IStorageFactory storageFactory,
    IStorageDriver storageDriver,
    IMediaAnalyzer mediaAnalyzer
) : IFileManager
{
    private IStorage StorageFor(Folder folder) =>
        storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);

    private int Id { get; set; }
    private Movie? Movie { get; set; }
    private Tv? Show { get; set; }

    private List<Folder> Folders { get; set; } = [];
    private List<MediaFolderExtend> Files { get; set; } = [];
    public string Type { get; set; } = "";

    private string? Filter { get; set; }

    public async Task<bool> FindFiles(int id, Library library)
    {
        Id = id;

        await MediaType(id: id, library: library);

        Folders = Paths(library: library, movie: Movie, show: Show);

        foreach (Folder folder in Folders)
        {
            // Pass the whole folder so GetFiles can resolve the right driver
            // (local / NFS / S3) for it. Hardcoding _storageDriver was
            // scanning every library against the local disk regardless of
            // its actual backend — NFS NAS and S3 buckets returned 0 files.
            ConcurrentBag<MediaFolderExtend> files = await GetFiles(library: library, folder: folder);

            if (!files.IsEmpty)
                Files.AddRange(collection: files);
        }

        // How many playable files the scan actually resolved. Logged next to the
        // per-type candidate count so an empty result is distinguishable from a
        // scan that found files but failed to parse them.
        int rawFileCount = Files.Sum(selector: folder => folder.Files?.Count ?? 0);
        bool hasCandidates = Files
            .SelectMany(selector: folder => folder.Files ?? [])
            .Any(predicate: file => file.Parsed is not null);

        Logger.App(
            message: $"[FindFiles] {Type} id={id}: scan resolved {rawFileCount} file(s), "
                     + $"{(hasCandidates ? "has" : "no")} parseable candidates across {Folders.Count} folder(s)",
            level: LogEventLevel.Information
        );

        // Delete old records first as a single committed step, then insert each
        // new record in its own SaveChangesAsync. A single wrapping transaction
        // around 80 NFS/S3 reads holds the SQLite writer lock for the entire
        // scan and hides every insert until commit, so partial progress is
        // invisible and the writer blocks every other workload.
        //
        // Only clear when the scan actually found replacements. A rescan that
        // comes back empty — a transient remote-storage hiccup, or a scan-side
        // regression — must NOT wipe a show/movie that is still fully on disk;
        // deleting here and then storing 0 leaves the library emptier than
        // before the rescan. Genuine on-disk deletions are reconciled by the
        // file-watcher's FileDeletedEvent path, not by nuking on every empty scan.
        if (Filter is null && hasCandidates)
        {
            switch (library.Type)
            {
                case MediaTypes.MovieMediaType:
                    await fileRepository.DeleteVideoFilesAndMetadataByMovieIdAsync(movieId: id);
                    break;
                case MediaTypes.TvMediaType:
                case MediaTypes.AnimeMediaType:
                    await fileRepository.DeleteVideoFilesAndMetadataByTvIdAsync(tvId: Show?.Id ?? id);
                    break;
            }
        }
        else if (Filter is null && !hasCandidates)
        {
            Logger.App(
                message: $"[FindFiles] {Type} id={id}: scan found no parseable files — preserving existing "
                         + "records instead of deleting (rescan is non-destructive on an empty result)",
                level: LogEventLevel.Warning
            );
        }

        switch (library.Type)
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
                Logger.App(message: "Unknown library type");
                break;
        }

        // Publish refresh events only after successful commit
        switch (library.Type)
        {
            case MediaTypes.MovieMediaType:
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        @event: new LibraryRefreshedEvent
                        {
                            QueryKey = ["libraries", library.Id.ToString()],
                        }
                    );
                    // Info-page invalidation: this is the choke point every
                    // Movie scan path (encoder finalize, manual rescan, initial
                    // import) runs through, so publishing here covers them all
                    // instead of duplicating the publish at each call site.
                    await EventBusProvider.Current.PublishAsync(
                        @event: new LibraryRefreshedEvent { QueryKey = ["movie", id.ToString()] }
                    );
                }
                break;
            case MediaTypes.TvMediaType:
            case MediaTypes.AnimeMediaType:
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        @event: new LibraryRefreshedEvent
                        {
                            QueryKey = ["libraries", library.Id.ToString()],
                        }
                    );
                    // Anime shows have no /anime/:id route on the client — they
                    // render at /tv/:id, so an anime-type library's info-page
                    // key is "tv", never "anime".
                    await EventBusProvider.Current.PublishAsync(
                        @event: new LibraryRefreshedEvent { QueryKey = ["tv", (Show?.Id ?? id).ToString()] }
                    );
                }
                break;
            case MediaTypes.MusicMediaType:
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(
                        @event: new LibraryRefreshedEvent { QueryKey = ["music"] }
                    );
                break;
        }

        return hasCandidates;
    }

    public void FilterFiles(string filter)
    {
        Filter = filter;
    }

    public async Task MoveToLibraryFolder(int id, Folder folder)
    {
        await using MediaContext context = new();

        Tv? tv = await context
            .Tvs.Include(navigationPropertyPath: tv => tv.Library)
                .ThenInclude(navigationPropertyPath: lib => lib.FolderLibraries)
                    .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .Include(navigationPropertyPath: tv => tv.Episodes)
                .ThenInclude(navigationPropertyPath: e => e.VideoFiles)
            .FirstOrDefaultAsync(predicate: t => t.Id == id);

        Movie? movie = await context
            .Movies.Include(navigationPropertyPath: movie => movie.Library)
                .ThenInclude(navigationPropertyPath: lib => lib.FolderLibraries)
                    .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .Include(navigationPropertyPath: movie => movie.VideoFiles)
            .FirstOrDefaultAsync(predicate: movie => movie.Id == id);

        string folderName = "";
        string sourceFolder = "";
        IStorage? sourceStorage = null;

        if (tv?.Folder is not null)
            foreach (FolderLibrary libraryFolder in tv.Library.FolderLibraries)
            {
                IStorage folderStorage = StorageFor(folder: libraryFolder.Folder);
                string folderRoot = ResolveBackendPath(storage: folderStorage, scopeRelativePath: libraryFolder.Folder.Path);
                string path = folderStorage.CombinePath(parent: folderRoot, child: tv.Folder);
                if (!folderStorage.Exists(path: path))
                {
                    string? match = FileNameSanitizer.FindMatchingDirectory(
                        driver: storageDriver,
                        rootPath: folderRoot,
                        expectedFolderName: tv.Folder.Replace(oldValue: "/", newValue: "")
                    );
                    if (match != null)
                        path = match;
                }

                if (!folderStorage.Exists(path: path))
                    continue;

                folderName = tv.Folder;
                sourceFolder = path;
                sourceStorage = folderStorage;

                break;
            }
        else if (movie?.Folder is not null)
            foreach (FolderLibrary libraryFolder in movie.Library.FolderLibraries)
            {
                IStorage folderStorage = StorageFor(folder: libraryFolder.Folder);
                string folderRoot = ResolveBackendPath(storage: folderStorage, scopeRelativePath: libraryFolder.Folder.Path);
                string path = folderStorage.CombinePath(parent: folderRoot, child: movie.Folder);
                if (!folderStorage.Exists(path: path))
                {
                    string? match = FileNameSanitizer.FindMatchingDirectory(
                        driver: storageDriver,
                        rootPath: folderRoot,
                        expectedFolderName: movie.Folder.Replace(oldValue: "/", newValue: "")
                    );
                    if (match != null)
                        path = match;
                }

                if (!folderStorage.Exists(path: path))
                    continue;

                folderName = movie.Folder;
                sourceFolder = path;
                sourceStorage = folderStorage;

                break;
            }

        if (
            string.IsNullOrEmpty(value: folderName)
            || string.IsNullOrEmpty(value: sourceFolder)
            || sourceStorage is null
        )
        {
            Logger.App(message: "Folder not found");
            return;
        }

        IStorage destinationStorage = StorageFor(folder: folder);
        string destinationRoot = ResolveBackendPath(storage: destinationStorage, scopeRelativePath: folder.Path);
        string destinationFolder = destinationStorage.CombinePath(parent: destinationRoot, child: folderName);

        Logger.App(message: $"Moving {sourceFolder} to {destinationFolder}");

        await MoveFolderAsync(sourceFolder: sourceFolder, destinationFolder: destinationFolder, sourceStorage: sourceStorage, destinationStorage: destinationStorage);

        FolderLibrary? newFolderLibrary = await context
            .FolderLibrary.Include(navigationPropertyPath: fl => fl.Library)
            .Include(navigationPropertyPath: fl => fl.Folder)
            .FirstOrDefaultAsync(predicate: fl => fl.FolderId == folder.Id);

        if (newFolderLibrary is null)
            return;

        if (tv?.Folder is not null)
        {
            tv.Folder = folderName;
            tv.LibraryId = newFolderLibrary.LibraryId;

            // LibraryTv.LibraryId is part of its composite primary key
            // (LibraryId, TvId) — EF Core rejects mutating a PK column on a
            // tracked entity ("part of a key and so cannot be modified").
            // Repointing to the new library is a delete of the old link row
            // plus an insert of the new one, never an in-place update.
            LibraryTv? libraryTv = await context.LibraryTv.FirstOrDefaultAsync(predicate: lt =>
                lt.TvId == tv.Id
            );

            if (libraryTv is not null)
            {
                context.LibraryTv.Remove(entity: libraryTv);
                context.LibraryTv.Add(entity: new(libraryId: newFolderLibrary.LibraryId, tvId: tv.Id));
            }

            await context.SaveChangesAsync();
        }
        else if (movie?.Folder is not null)
        {
            movie.Folder = folderName;
            movie.LibraryId = newFolderLibrary.LibraryId;

            // See the LibraryTv comment above — LibraryMovie.LibraryId is
            // equally part of its composite primary key.
            LibraryMovie? libraryMovie = await context.LibraryMovie.FirstOrDefaultAsync(predicate: lm =>
                lm.MovieId == movie.Id
            );

            if (libraryMovie is not null)
            {
                context.LibraryMovie.Remove(entity: libraryMovie);
                context.LibraryMovie.Add(entity: new(libraryId: newFolderLibrary.LibraryId, movieId: movie.Id));
            }

            await context.SaveChangesAsync();
        }

        _ = await FindFiles(id: id, library: newFolderLibrary.Library);
    }
}
