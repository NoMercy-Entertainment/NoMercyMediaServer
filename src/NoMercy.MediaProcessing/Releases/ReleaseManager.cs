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

using Microsoft.Extensions.Logging;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Releases;

public class ReleaseManager(
    IReleaseRepository releaseRepository,
    IMusicGenreRepository musicGenreRepository,
    IStorageFactory storageFactory,
    JobDispatcher jobDispatcher,
    ILogger<ReleaseManager> logger
) : BaseManager, IReleaseManager
{
    public async Task<(
        MusicBrainzReleaseAppends? releaseAppends,
        CoverArtImageManagerManager.CoverPalette? coverPalette
    )> Add(Guid id, Library albumLibrary, Folder libraryFolder, MediaFolder mediaFolder)
    {
        logger.LogTrace(message: "Adding Release: {Id} to Library: {Title}", args: [id, albumLibrary.Title]);

        MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        MusicBrainzReleaseAppends? releaseAppends = await musicBrainzReleaseClient.WithAllAppends(
            id: id
        );

        if (releaseAppends == null)
            return (null, null);

        CoverArtImageManagerManager.CoverPalette? coverPalette =
            await CoverArtImageManagerManager.Add(id: releaseAppends.MusicBrainzReleaseGroup.Id);

        if (coverPalette is not null)
        {
            using Image<Rgba32>? downloadedImage = await CoverArtCoverArtClient.Download(
                url: coverPalette.Url
            );
        }

        await Store(releaseAppends: releaseAppends, library: albumLibrary, libraryFolder: libraryFolder, mediaFolder: mediaFolder, coverPalette: coverPalette);

        return (releaseAppends, coverPalette);
    }

    private async Task Store(
        MusicBrainzReleaseAppends releaseAppends,
        Library library,
        Folder libraryFolder,
        MediaFolder mediaFolder,
        CoverArtImageManagerManager.CoverPalette? coverPalette
    )
    {
        try
        {
            logger.LogTrace(message: "Storing Release: {Title}", args: releaseAppends.Title);

            string libraryRoot = ResolveLibraryRoot(libraryFolder: libraryFolder);
            string folder = mediaFolder.Path.Replace(oldValue: libraryRoot, newValue: "");

            Album release = new()
            {
                Id = releaseAppends.Id,
                Name = releaseAppends.Title,
                Country = releaseAppends.Country,
                Disambiguation = string.IsNullOrEmpty(value: releaseAppends.Disambiguation)
                    ? null
                    : releaseAppends.Disambiguation,
                Year = releaseAppends.DateTime?.Year ?? 0,
                Tracks = releaseAppends.Media.Sum(selector: m => m.TrackCount),

                LibraryId = library.Id,
                FolderId = libraryFolder.Id,
                HostFolder = folder.PathName(),

                Folder = folder.Replace(oldValue: "\\", newValue: "/"),

                Cover = coverPalette?.Url is not null ? $"/{coverPalette.Url.FileName()}" : null,

                _colorPalette = (coverPalette?.Palette).OrEmpty(),
            };

            await releaseRepository.Store(release: release);
            jobDispatcher.DispatchColorPaletteJob(entityType: "album", entityId: release.Id.ToString());

            await LinkToLibrary(releaseAppends: releaseAppends, library: library);

            List<AlbumMusicGenre> genres = releaseAppends
                .Genres.Select(selector: genre => new AlbumMusicGenre
                {
                    AlbumId = releaseAppends.Id,
                    MusicGenreId = genre.Id,
                })
                .ToList();

            await musicGenreRepository.LinkToRelease(genreReleases: genres);

            logger.LogTrace(message: "Release {Title} stored", args: releaseAppends.Title);
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
        }
    }

    private async Task LinkToLibrary(MusicBrainzReleaseAppends releaseAppends, Library library)
    {
        logger.LogTrace(message: "Linking Release to Library: {Title}", args: releaseAppends.Title);

        AlbumLibrary insert = new() { AlbumId = releaseAppends.Id, LibraryId = library.Id };

        await releaseRepository.LinkToLibrary(albumLibrary: insert);
    }

    public async Task Store(
        MusicBrainzReleaseAppends releaseAppends,
        Library library,
        Folder libraryFolder,
        MediaFile mediaFile,
        CoverArtImageManagerManager.CoverPalette? coverPalette
    )
    {
        try
        {
            logger.LogTrace(message: "Storing Release: {Title}", args: releaseAppends.Title);

            string libraryRoot = ResolveLibraryRoot(libraryFolder: libraryFolder);
            string folder = StoragePathHelpers
                .GetParent(path: mediaFile.Path.Replace(oldValue: libraryRoot, newValue: ""))
                .OrEmpty();

            Album release = new()
            {
                Id = releaseAppends.Id,
                Name = releaseAppends.Title,
                Country = releaseAppends.Country,
                Disambiguation = string.IsNullOrEmpty(value: releaseAppends.Disambiguation)
                    ? null
                    : releaseAppends.Disambiguation,
                Year = releaseAppends.DateTime?.Year ?? 0,
                Tracks = releaseAppends.Media.Sum(selector: m => m.TrackCount),

                LibraryId = library.Id,
                FolderId = libraryFolder.Id,
                HostFolder = folder.PathName(),

                Folder = folder.Replace(oldValue: "\\", newValue: "/"),

                Cover = coverPalette?.Url is not null ? $"/{coverPalette.Url.FileName()}" : null,

                _colorPalette = (coverPalette?.Palette).OrEmpty(),
            };

            await releaseRepository.Store(release: release);
            jobDispatcher.DispatchColorPaletteJob(entityType: "album", entityId: release.Id.ToString());

            await LinkToLibrary(releaseAppends: releaseAppends, library: library);
            await LinkToReleaseGroup(releaseAppends: releaseAppends);
            await LinkToGenre(releaseAppends: releaseAppends);

            logger.LogTrace(message: "Release {Title} stored", args: releaseAppends.Title);
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
        }
    }

    private async Task LinkToGenre(MusicBrainzReleaseAppends releaseAppends)
    {
        List<AlbumMusicGenre> genres = releaseAppends
            .Genres.Select(selector: genre => new AlbumMusicGenre
            {
                AlbumId = releaseAppends.Id,
                MusicGenreId = genre.Id,
            })
            .ToList();

        await musicGenreRepository.LinkToRelease(genreReleases: genres);
    }

    private async Task LinkToReleaseGroup(MusicBrainzReleaseAppends releaseAppends)
    {
        logger.LogTrace(message: "Linking Release to Release Group: {Title}", args: releaseAppends.Title);

        AlbumReleaseGroup insert = new()
        {
            AlbumId = releaseAppends.Id,
            ReleaseGroupId = releaseAppends.MusicBrainzReleaseGroup.Id,
        };

        await releaseRepository.LinkToReleaseGroup(albumReleaseGroup: insert);
    }

    private string ResolveLibraryRoot(Folder libraryFolder)
    {
        IStorage folderStorage = storageFactory.For(
            folderId: libraryFolder.Id,
            driverId: libraryFolder.DriverId,
            subPath: string.Empty
        );
        return FolderRootPath(storage: folderStorage, path: libraryFolder.Path);
    }
}
