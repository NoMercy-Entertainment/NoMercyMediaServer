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
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Artists;

public class ArtistManager(
    IArtistRepository artistRepository,
    IMusicGenreRepository musicGenreRepository,
    JobDispatcher jobDispatcher,
    IStorageFactory storageFactory,
    ILogger<ArtistManager> logger
) : BaseManager, IArtistManager
{
    /** this is the store for a Release artist */
    public async Task Store(
        ReleaseArtistCredit artistCredit,
        Library library,
        Folder libraryFolder,
        MediaFolder mediaFolder,
        MusicBrainzReleaseAppends releaseAppends
    )
    {
        logger.LogTrace(message: "Storing Artist: {Name}", args: artistCredit.MusicBrainzArtist.Name);
        string artistFolder = MakeArtistFolder(artist: artistCredit.MusicBrainzArtist.Name);
        string folder = mediaFolder.Path.Replace(oldValue: ResolveLibraryRoot(libraryFolder: libraryFolder), newValue: "");

        Artist artist = new()
        {
            Id = artistCredit.MusicBrainzArtist.Id,
            Name = artistCredit.MusicBrainzArtist.Name,
            Disambiguation = string.IsNullOrEmpty(value: artistCredit.MusicBrainzArtist.Disambiguation)
                ? null
                : artistCredit.MusicBrainzArtist.Disambiguation,
            Country = artistCredit.MusicBrainzArtist.Country,
            TitleSort = string.IsNullOrEmpty(value: artistCredit.MusicBrainzArtist.SortName)
                ? artistCredit.MusicBrainzArtist.Name.TitleSort()
                : artistCredit.MusicBrainzArtist.SortName,

            LibraryId = library.Id,
            FolderId = libraryFolder.Id,

            Folder = artistFolder,
            HostFolder = folder.PathName(),
        };

        await artistRepository.StoreAsync(artist: artist);
        jobDispatcher.DispatchColorPaletteJob(entityType: "artist", entityId: artist.Id.ToString());

        await LinkToLibrary(artistMusicBrainzArtist: artistCredit.MusicBrainzArtist, library: library);
        await LinkToRelease(artistMusicBrainzArtist: artistCredit.MusicBrainzArtist, releaseAppends: releaseAppends);

        try
        {
            List<ArtistMusicGenre> genres = artistCredit
                .MusicBrainzArtist.Genres.Select(selector: genre => new ArtistMusicGenre
                {
                    ArtistId = artistCredit.MusicBrainzArtist.Id,
                    MusicGenreId = genre.Id,
                })
                .ToList();

            await musicGenreRepository.LinkToArtist(genreArtists: genres);
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
        }

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent
                {
                    QueryKey = ["music", "artist", artistCredit.MusicBrainzArtist.Id.ToString()],
                }
            );
    }

    /** this is the store for a Release artist */
    public async Task Store(
        MusicBrainzArtistAppends artistCredit,
        MusicBrainzReleaseAppends releaseAppends,
        Library library,
        Folder libraryFolder
    )
    {
        logger.LogTrace(message: "Storing Artist: {Name}", args: artistCredit.Name);
        string artistFolder = MakeArtistFolder(artist: artistCredit.Name);
        string folder = artistFolder.Replace(oldValue: "/", newValue: StringExtensions.DirectorySeparator);

        CoverArtImageManagerManager.CoverPalette? coverPalette = await GetCoverArtForArtist(
            artistCredit: artistCredit
        );

        Artist artist = new()
        {
            Id = artistCredit.Id,
            Name = artistCredit.Name,
            Disambiguation = string.IsNullOrEmpty(value: artistCredit.Disambiguation)
                ? null
                : artistCredit.Disambiguation,
            Cover = coverPalette?.Url is not null ? $"/{coverPalette.Url.FileName()}" : null,
            Country = artistCredit.Country,
            TitleSort = string.IsNullOrEmpty(value: artistCredit.SortName)
                ? artistCredit.Name.TitleSort()
                : artistCredit.SortName,

            LibraryId = library.Id,
            FolderId = libraryFolder.Id,

            Folder = artistFolder,
            HostFolder = folder.PathName(),
        };

        await artistRepository.StoreAsync(artist: artist);
        jobDispatcher.DispatchColorPaletteJob(entityType: "artist", entityId: artist.Id.ToString());
        jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzArtist: artistCredit);

        await LinkToLibrary(artistMusicBrainzArtist: artistCredit, library: library);
        await LinkToRelease(artistMusicBrainzArtist: artistCredit, releaseAppends: releaseAppends);

        foreach (MusicBrainzGenreDetails musicBrainzGenreDetails in artistCredit.Genres)
        {
            MusicGenre musicGenre = new()
            {
                Id = musicBrainzGenreDetails.Id,
                Name = musicBrainzGenreDetails.Name,
            };
            await musicGenreRepository.Store(musicGenre: musicGenre);
        }
        try
        {
            List<ArtistMusicGenre> genres = artistCredit
                .Genres.Select(selector: genre => new ArtistMusicGenre
                {
                    ArtistId = artistCredit.Id,
                    MusicGenreId = genre.Id,
                })
                .ToList();

            await musicGenreRepository.LinkToArtist(genreArtists: genres);
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
        }

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent
                {
                    QueryKey = ["music", "artist", artistCredit.Id.ToString()],
                }
            );
    }

    /** this is the store for a Recording artist */
    public async Task Store(
        MusicBrainzArtistDetails artistCredit,
        Library library,
        Folder libraryFolder,
        MediaFolder mediaFolder,
        MusicBrainzTrack track
    )
    {
        logger.LogTrace(message: "Storing Artist: {Name}", args: artistCredit.Name);
        string artistFolder = MakeArtistFolder(artist: artistCredit.Name);
        string folder = mediaFolder.Path.Replace(oldValue: ResolveLibraryRoot(libraryFolder: libraryFolder), newValue: "");

        CoverArtImageManagerManager.CoverPalette? coverPalette = await GetCoverArtForArtist(
            artistCredit: artistCredit
        );

        Artist artist = new()
        {
            Id = artistCredit.Id,
            Name = artistCredit.Name,
            Disambiguation = string.IsNullOrEmpty(value: artistCredit.Disambiguation)
                ? null
                : artistCredit.Disambiguation,
            Cover = coverPalette?.Url is not null ? $"/{coverPalette.Url.FileName()}" : null,
            Country = artistCredit.Country,
            TitleSort = string.IsNullOrEmpty(value: artistCredit.SortName)
                ? artistCredit.Name.TitleSort()
                : artistCredit.SortName,

            LibraryId = library.Id,
            FolderId = libraryFolder.Id,

            Folder = artistFolder,
            HostFolder = folder.PathName(),
        };

        await artistRepository.StoreAsync(artist: artist);
        jobDispatcher.DispatchColorPaletteJob(entityType: "artist", entityId: artist.Id.ToString());
        jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzArtist: artistCredit);

        await LinkToLibrary(artistMusicBrainzArtist: artistCredit, library: library);
        await LinkToTrack(artistCredit: artistCredit, track: track);
        foreach (MusicBrainzGenreDetails musicBrainzGenreDetails in artistCredit.Genres)
        {
            MusicGenre musicGenre = new()
            {
                Id = musicBrainzGenreDetails.Id,
                Name = musicBrainzGenreDetails.Name,
            };
            await musicGenreRepository.Store(musicGenre: musicGenre);
        }
        try
        {
            List<ArtistMusicGenre> genres = artistCredit
                .Genres.Select(selector: genre => new ArtistMusicGenre
                {
                    ArtistId = artistCredit.Id,
                    MusicGenreId = genre.Id,
                })
                .ToList();

            await musicGenreRepository.LinkToArtist(genreArtists: genres);
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
        }

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent
                {
                    QueryKey = ["music", "artist", artistCredit.Id.ToString()],
                }
            );
    }

    private static async Task<CoverArtImageManagerManager.CoverPalette?> GetCoverArtForArtist(
        MusicBrainzArtistDetails artistCredit
    )
    {
        CoverArtImageManagerManager.CoverPalette? coverPalette = await FanArtImageManager.Add(
            id: artistCredit.Id,
            priority: true
        );

        if (coverPalette is not null)
        {
            using Image<Rgba32>? downloadedImage = await FanArtImageClient.Download(
                url: coverPalette.Url!
            );
        }

        return coverPalette;
    }

    private async Task LinkToTrack(MusicBrainzArtistDetails artistCredit, MusicBrainzTrack track)
    {
        logger.LogTrace(message: "Linking Artist to Track: {Name}", args: artistCredit.Name);

        ArtistTrack insert = new() { ArtistId = artistCredit.Id, TrackId = track.Id };

        await artistRepository.LinkToRecording(insert: insert);
    }

    private async Task LinkToRelease(
        MusicBrainzArtistDetails artistMusicBrainzArtist,
        MusicBrainzReleaseAppends releaseAppends
    )
    {
        logger.LogTrace(message: "Linking Artist to Release: {Name}", args: artistMusicBrainzArtist.Name);

        AlbumArtist insert = new()
        {
            ArtistId = artistMusicBrainzArtist.Id,
            AlbumId = releaseAppends.Id,
        };

        await artistRepository.LinkToRelease(insert: insert);
    }

    private async Task LinkToLibrary(
        MusicBrainzArtistDetails artistMusicBrainzArtist,
        Library library
    )
    {
        logger.LogTrace(message: "Linking Artist to Library: {Name}", args: artistMusicBrainzArtist.Name);

        ArtistLibrary insert = new()
        {
            ArtistId = artistMusicBrainzArtist.Id,
            LibraryId = library.Id,
        };

        await artistRepository.LinkToLibrary(insert: insert);
    }

    private static string MakeArtistFolder(string artist)
    {
        string artistName = artist.RemoveDiacritics();

        string artistFolder = char.IsNumber(c: artistName[index: 0])
            ? "#"
            : artistName[index: 0].ToString().ToUpper();

        return $"/{artistFolder}/{artistName}";
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
