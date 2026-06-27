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
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Domain;
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
    private IStorage StorageFor(Folder folder) =>
        storageFactory.For(folder.Id, folder.DriverId, string.Empty);

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

        // Delete old records first as a single committed step, then insert each
        // new record in its own SaveChangesAsync. A single wrapping transaction
        // around 80 NFS/S3 reads holds the SQLite writer lock for the entire
        // scan and hides every insert until commit, so partial progress is
        // invisible and the writer blocks every other workload.
        if (Filter is null)
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
            case MediaTypes.TvMediaType:
            case MediaTypes.AnimeMediaType:
                if (EventBusProvider.IsConfigured)
                    await EventBusProvider.Current.PublishAsync(
                        new LibraryRefreshEvent { QueryKey = ["libraries", library.Id.ToString()] }
                    );
                break;
            case MediaTypes.MusicMediaType:
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
        IStorage? sourceStorage = null;

        if (tv?.Folder is not null)
            foreach (FolderLibrary libraryFolder in tv.Library.FolderLibraries)
            {
                IStorage folderStorage = StorageFor(libraryFolder.Folder);
                string folderRoot = folderStorage.GetFullPath(libraryFolder.Folder.Path);
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
                string folderRoot = folderStorage.GetFullPath(libraryFolder.Folder.Path);
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
        string destinationRoot = destinationStorage.GetFullPath(folder.Path);
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
}
