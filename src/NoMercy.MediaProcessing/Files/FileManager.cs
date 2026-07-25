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
        storageFactory.For(folder.Id, folder.DriverId, string.Empty);

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

        // How many playable files the scan actually resolved. Logged next to the
        // per-type candidate count so an empty result is distinguishable from a
        // scan that found files but failed to parse them.
        int rawFileCount = Files.Sum(folder => folder.Files?.Count ?? 0);
        bool hasCandidates = Files
            .SelectMany(folder => folder.Files ?? [])
            .Any(file => file.Parsed is not null);

        Logger.App(
            $"[FindFiles] {Type} id={id}: scan resolved {rawFileCount} file(s), "
                     + $"{(hasCandidates ? "has" : "no")} parseable candidates across {Folders.Count} folder(s)",
            LogEventLevel.Information
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
                    await fileRepository.DeleteVideoFilesAndMetadataByMovieIdAsync(id);
                    break;
                case MediaTypes.TvMediaType:
                case MediaTypes.AnimeMediaType:
                    await fileRepository.DeleteVideoFilesAndMetadataByTvIdAsync(Show?.Id ?? id);
                    break;
            }
        }
        else if (Filter is null && !hasCandidates)
        {
            Logger.App(
                $"[FindFiles] {Type} id={id}: scan found no parseable files — preserving existing "
                         + "records instead of deleting (rescan is non-destructive on an empty result)",
                LogEventLevel.Warning
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
                Logger.App("Unknown library type");
                break;
        }

        // Publish refresh events only after successful commit
        switch (library.Type)
        {
            case MediaTypes.MovieMediaType:
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshedEvent
                        {
                            QueryKey = ["libraries", library.Id.ToString()],
                        }
                    );
                    // Info-page invalidation: this is the choke point every
                    // Movie scan path (encoder finalize, manual rescan, initial
                    // import) runs through, so publishing here covers them all
                    // instead of duplicating the publish at each call site.
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshedEvent { QueryKey = ["movie", id.ToString()] }
                    );
                }
                break;
            case MediaTypes.TvMediaType:
            case MediaTypes.AnimeMediaType:
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshedEvent
                        {
                            QueryKey = ["libraries", library.Id.ToString()],
                        }
                    );
                    // Anime shows have no /anime/:id route on the client — they
                    // render at /tv/:id, so an anime-type library's info-page
                    // key is "tv", never "anime".
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshedEvent { QueryKey = ["tv", (Show?.Id ?? id).ToString()] }
                    );
                }
                break;
            case MediaTypes.MusicMediaType:
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshedEvent { QueryKey = ["music"] }
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
        IStorage? sourceStorage = null;

        if (tv?.Folder is not null)
            foreach (FolderLibrary libraryFolder in tv.Library.FolderLibraries)
            {
                IStorage folderStorage = StorageFor(libraryFolder.Folder);
                string folderRoot = ResolveBackendPath(folderStorage, libraryFolder.Folder.Path);
                string path = folderStorage.CombinePath(folderRoot, tv.Folder);
                if (!folderStorage.Exists(path))
                {
                    string? match = FileNameSanitizer.FindMatchingDirectory(
                        storageDriver,
                        folderRoot,
                        tv.Folder.Replace("/", "")
                    );
                    if (match != null)
                        path = match;
                }

                if (!folderStorage.Exists(path))
                    continue;

                folderName = tv.Folder;
                sourceFolder = path;
                sourceStorage = folderStorage;

                break;
            }
        else if (movie?.Folder is not null)
            foreach (FolderLibrary libraryFolder in movie.Library.FolderLibraries)
            {
                IStorage folderStorage = StorageFor(libraryFolder.Folder);
                string folderRoot = ResolveBackendPath(folderStorage, libraryFolder.Folder.Path);
                string path = folderStorage.CombinePath(folderRoot, movie.Folder);
                if (!folderStorage.Exists(path))
                {
                    string? match = FileNameSanitizer.FindMatchingDirectory(
                        storageDriver,
                        folderRoot,
                        movie.Folder.Replace("/", "")
                    );
                    if (match != null)
                        path = match;
                }

                if (!folderStorage.Exists(path))
                    continue;

                folderName = movie.Folder;
                sourceFolder = path;
                sourceStorage = folderStorage;

                break;
            }

        if (
            string.IsNullOrEmpty(folderName)
            || string.IsNullOrEmpty(sourceFolder)
            || sourceStorage is null
        )
        {
            Logger.App("Folder not found");
            return;
        }

        IStorage destinationStorage = StorageFor(folder);
        string destinationRoot = ResolveBackendPath(destinationStorage, folder.Path);
        string destinationFolder = destinationStorage.CombinePath(destinationRoot, folderName);

        Logger.App($"Moving {sourceFolder} to {destinationFolder}");

        await MoveFolderAsync(sourceFolder, destinationFolder, sourceStorage, destinationStorage);

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

            // LibraryTv.LibraryId is part of its composite primary key
            // (LibraryId, TvId) — EF Core rejects mutating a PK column on a
            // tracked entity ("part of a key and so cannot be modified").
            // Repointing to the new library is a delete of the old link row
            // plus an insert of the new one, never an in-place update.
            LibraryTv? libraryTv = await context.LibraryTv.FirstOrDefaultAsync(lt =>
                lt.TvId == tv.Id
            );

            if (libraryTv is not null)
            {
                context.LibraryTv.Remove(libraryTv);
                context.LibraryTv.Add(new(newFolderLibrary.LibraryId, tv.Id));
            }

            await context.SaveChangesAsync();
        }
        else if (movie?.Folder is not null)
        {
            movie.Folder = folderName;
            movie.LibraryId = newFolderLibrary.LibraryId;

            // See the LibraryTv comment above — LibraryMovie.LibraryId is
            // equally part of its composite primary key.
            LibraryMovie? libraryMovie = await context.LibraryMovie.FirstOrDefaultAsync(lm =>
                lm.MovieId == movie.Id
            );

            if (libraryMovie is not null)
            {
                context.LibraryMovie.Remove(libraryMovie);
                context.LibraryMovie.Add(new(newFolderLibrary.LibraryId, movie.Id));
            }

            await context.SaveChangesAsync();
        }

        _ = await FindFiles(id, newFolderLibrary.Library);
    }
}
