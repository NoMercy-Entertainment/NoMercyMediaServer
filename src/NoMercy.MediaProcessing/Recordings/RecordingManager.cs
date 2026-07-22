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
using Microsoft.Extensions.Logging;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Artists;
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

namespace NoMercy.MediaProcessing.Recordings;

public partial class RecordingManager(
    IRecordingRepository recordingRepository,
    IMusicGenreRepository musicGenreRepository,
    IArtistRepository artistRepository,
    IStorageDriver storageDriver,
    IStorageFactory storageFactory,
    ILogger<RecordingManager> logger
) : BaseManager, IRecordingManager
{
    public async Task<bool> Store(
        MusicBrainzReleaseAppends releaseAppends,
        MusicBrainzTrack musicBrainzTrack,
        MusicBrainzMedia musicBrainzMedia,
        Folder libraryFolder,
        MediaFolder mediaFolder,
        CoverArtImageManagerManager.CoverPalette? releaseCoverPalette
    )
    {
        logger.LogTrace(
            message: "Storing Recording: {Title} - {Position}-{Position2} {Title2}", args: [releaseAppends.Title, musicBrainzMedia.Position, musicBrainzTrack.Position, musicBrainzTrack.Title]
        );

        MediaScan mediaScan = new(driver: storageDriver);
        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType(mediaType: "music")
            .Process(rootFolder: mediaFolder.Path, depth: 1);

        foreach (MediaFolderExtend folder in folders)
        {
            if (folder.Files is null || folder.Files.IsEmpty)
                continue;
            foreach (MediaFile file in folder.Files)
            {
                MediaFile? mediaFile = FileMatch(
                    inputFile: file,
                    musicBrainzRelease: releaseAppends,
                    musicBrainzMedia: musicBrainzMedia,
                    trackNumber: musicBrainzTrack.Position
                );
                if (mediaFile is null)
                    continue;

                TagFile? tagFile = file.TagFile;
                if (tagFile == null || mediaFile.FFprobe == null)
                {
                    logger.LogError(message: "File not found: {Name}", args: file.Name);
                    continue;
                }

                logger.LogTrace(message: "Recording {Title} found", args: musicBrainzTrack.Title);

                string path =
                    mediaFile
                        .Parsed?.FilePath.Replace(oldValue: "/" + mediaFile.Name, newValue: "")
                        .Replace(oldValue: "\\" + mediaFile.Name, newValue: "")
                    ?? string.Empty;

                Track insert = new()
                {
                    Id = musicBrainzTrack.Id,
                    Name = musicBrainzTrack.Title,
                    Date =
                        releaseAppends.DateTime
                        ?? releaseAppends.ReleaseEvents?.FirstOrDefault()?.DateTime,
                    DiscNumber = musicBrainzMedia.Position,
                    TrackNumber = musicBrainzTrack.Position,

                    Filename = "/" + StoragePathHelpers.GetName(path: mediaFile.Path.Replace(oldChar: '\\', newChar: '/')),
                    Quality = (int)
                        Math.Floor(
                            d: (
                                (double?)mediaFile.FFprobe?.Format.BitRate
                                ?? (double?)(tagFile!.Properties?.AudioBitrate * 1000)
                                ?? 0.0
                            ) / 1000.0
                        ),
                    Duration = HmsRegex()
                        .Replace(
                            input: (
                                mediaFile.FFprobe?.Duration
                                ?? tagFile!.Properties?.Duration
                                ?? TimeSpan.Zero
                            ).ToString(format: @"hh\:mm\:ss"),
                            replacement: ""
                        ),

                    FolderId = libraryFolder.Id,
                    Folder = path.Replace(oldValue: ResolveLibraryRoot(libraryFolder: libraryFolder), newValue: "").Replace(oldValue: "\\", newValue: "/"),
                    HostFolder = path.PathName(),

                    Cover = releaseCoverPalette?.Url is not null
                        ? $"/{releaseCoverPalette.Url.FileName()}"
                        : null,
                };

                await recordingRepository.Store(recording: insert);

                await LinkToRelease(musicBrainzTrack: musicBrainzTrack, releaseAppends: releaseAppends);
                await LinkToLibrary(
                    track: musicBrainzTrack,
                    library: libraryFolder.FolderLibraries.FirstOrDefault()!.Library
                );

                List<MusicGenreTrack> genres =
                    musicBrainzTrack
                        .Genres?.Select(selector: genre => new MusicGenreTrack
                        {
                            TrackId = musicBrainzTrack.Id,
                            GenreId = genre.Id,
                        })
                        .ToList()
                    ?? [];

                await musicGenreRepository.LinkToRecording(genreRecordings: genres);

                new JobDispatcher().DispatchColorPaletteJob(entityType: "track", entityId: insert.Id.ToString());

                logger.LogTrace(message: "Recording {Title} stored", args: musicBrainzTrack.Title);

                return true;
            }
        }

        return false;
    }

    private async Task LinkToRelease(
        MusicBrainzTrack musicBrainzTrack,
        MusicBrainzReleaseAppends releaseAppends
    )
    {
        logger.LogTrace(
            message: "Linking Recording to Artist: {Title} - {Title2}", args: [musicBrainzTrack.Title, releaseAppends.MusicBrainzReleaseGroup.Title]
        );

        foreach (ReleaseArtistCredit credit in releaseAppends.ArtistCredit)
        {
            ArtistTrack insert = new()
            {
                ArtistId = credit.MusicBrainzArtist.Id,
                TrackId = musicBrainzTrack.Id,
            };

            await recordingRepository.LinkToArtist(insert: insert);
        }
    }

    private async Task LinkToRelease(Track track, MusicBrainzReleaseAppends releaseAppends)
    {
        logger.LogTrace(message: "Linking Recording to Release: {Title}", args: releaseAppends.Title);

        AlbumTrack insert = new() { AlbumId = releaseAppends.Id, TrackId = track.Id };

        await recordingRepository.LinkToRelease(trackRelease: insert);
    }

    private async Task LinkToLibrary(MusicBrainzTrack track, Library library)
    {
        logger.LogTrace(message: "Linking Recording to Library: {Title}", args: track.Title);

        LibraryTrack insert = new() { LibraryId = library.Id, TrackId = track.Id };

        await recordingRepository.LinkToLibrary(libraryTrack: insert);
    }

    private async Task LinkToLibrary(Track track, Library library)
    {
        logger.LogTrace(message: "Linking Recording to Library: {Title}", args: library.Title);

        LibraryTrack insert = new() { LibraryId = library.Id, TrackId = track.Id };

        await recordingRepository.LinkToLibrary(libraryTrack: insert);
    }

    private MediaFile? FileMatch(
        MediaFile inputFile,
        MusicBrainzReleaseAppends musicBrainzRelease,
        MusicBrainzMedia musicBrainzMedia,
        int trackNumber
    )
    {
        bool hasMatch = FindTrackWithAlbumNumberByNumberPadded(
            inputFile: inputFile,
            musicBrainzMedia: musicBrainzMedia,
            hasMatch: false,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: trackNumber,
            padding: 4
        );
        hasMatch = FindTrackWithAlbumNumberByNumberPadded(
            inputFile: inputFile,
            musicBrainzMedia: musicBrainzMedia,
            hasMatch: hasMatch,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: trackNumber,
            padding: 3
        );
        hasMatch = FindTrackWithAlbumNumberByNumberPadded(
            inputFile: inputFile,
            musicBrainzMedia: musicBrainzMedia,
            hasMatch: hasMatch,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: trackNumber
        );

        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(
            inputFile: inputFile,
            musicBrainzMedia: musicBrainzMedia,
            hasMatch: hasMatch,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: trackNumber,
            padding: 4
        );
        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(
            inputFile: inputFile,
            musicBrainzMedia: musicBrainzMedia,
            hasMatch: hasMatch,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: trackNumber,
            padding: 3
        );
        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(
            inputFile: inputFile,
            musicBrainzMedia: musicBrainzMedia,
            hasMatch: hasMatch,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: trackNumber
        );

        if (!hasMatch)
            return null;
        return inputFile;
    }

    private bool FindTrackWithoutAlbumNumberByNumberPadded(
        MediaFile inputFile,
        MusicBrainzMedia musicBrainzMedia,
        bool hasMatch,
        int numberOfAlbums,
        int trackNumber,
        int padding = 2
    )
    {
        if (hasMatch)
            return true;
        if (numberOfAlbums > 1)
            return false;
        if (inputFile.Parsed is null)
            return false;

        string fileName = Path.GetFileName(path: inputFile.Parsed.FilePath)
            .RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower();

        string matchNumber = $"{trackNumber.ToString().PadLeft(totalWidth: padding, paddingChar: '0')} ";
        if (musicBrainzMedia.Tracks.Length < trackNumber)
            return false;
        string matchString = musicBrainzMedia
            .Tracks[trackNumber - 1]
            .Title.RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower()
            .Replace(oldValue: ".mp3", newValue: "");

        return fileName.StartsWith(value: matchNumber) && fileName.Contains(value: matchString);
    }

    private bool FindTrackWithAlbumNumberByNumberPadded(
        MediaFile inputFile,
        MusicBrainzMedia musicBrainzMedia,
        bool hasMatch,
        int numberOfAlbums,
        int trackNumber,
        int padding = 2
    )
    {
        if (hasMatch)
            return true;
        if (numberOfAlbums == 1)
            return false;
        if (inputFile.Parsed is null)
            return false;

        string fileName = Path.GetFileName(path: inputFile.Parsed.FilePath)
            .RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower();

        string matchNumber =
            $"{musicBrainzMedia.Position}-{trackNumber.ToString().PadLeft(totalWidth: padding, paddingChar: '0')} ";
        if (musicBrainzMedia.Tracks.Length < trackNumber)
            return false;
        string matchString = musicBrainzMedia
            .Tracks[trackNumber - 1]
            .Title.RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower()
            .Replace(oldValue: ".mp3", newValue: "");

        return fileName.StartsWith(value: matchNumber) && fileName.Contains(value: matchString);
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

    [GeneratedRegex(pattern: "^00:")]
    private static partial Regex HmsRegex();

    public async Task Store(
        MusicBrainzReleaseAppends releaseAppends,
        MusicBrainzTrack trackAppends,
        MusicBrainzArtistAppends[] artistAppends,
        MediaFile mediaFile,
        Folder libraryFolder,
        CoverArtImageManagerManager.CoverPalette? releaseCoverPalette
    )
    {
        JobDispatcher jobDispatcher = new();
        logger.LogTrace(message: "Recording {Title} found", args: releaseAppends.Title);

        foreach (MusicBrainzArtistAppends artist in artistAppends)
        {
            try
            {
                CoverArtImageManagerManager.CoverPalette? coverPalette =
                    await FanArtImageManager.Add(id: artist.Id, priority: true);

                if (coverPalette is not null)
                {
                    using Image<Rgba32>? downloadedImage = await FanArtImageClient.Download(
                        url: coverPalette.Url!
                    );
                }

                Artist artistEntity = new()
                {
                    Id = artist.Id,
                    Name = artist.Name,
                    Disambiguation = artist.Disambiguation,
                    Cover = coverPalette?.Url is not null
                        ? $"/{coverPalette.Url.FileName()}"
                        : null,
                    TitleSort = artist.SortName,
                    Country = artist.Country,
                    Year = artist.LifeSpan?.BeginDate?.Year,

                    FolderId = libraryFolder.Id,
                    Folder = mediaFile
                        .Parsed?.FilePath.Replace(oldValue: "/" + mediaFile.Name, newValue: "")
                        .Replace(oldValue: "\\" + mediaFile.Name, newValue: "")
                        .Replace(oldValue: ResolveLibraryRoot(libraryFolder: libraryFolder), newValue: "")
                        .Replace(oldValue: "\\", newValue: "/"),
                    HostFolder = mediaFile
                        .Parsed?.FilePath.Replace(oldValue: "/" + mediaFile.Name, newValue: "")
                        .Replace(oldValue: "\\" + mediaFile.Name, newValue: "")
                        .PathName()!,

                    LibraryId = libraryFolder.FolderLibraries.FirstOrDefault()!.LibraryId,
                };

                await artistRepository.StoreAsync(artist: artistEntity);
                jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzArtist: artist);
            }
            catch (Exception e)
            {
                logger.LogError(exception: e, message: "Failed to store recording artist metadata");
            }
        }

        string path =
            mediaFile
                .Parsed?.FilePath.Replace(oldValue: "/" + mediaFile.Name, newValue: "")
                .Replace(oldValue: "\\" + mediaFile.Name, newValue: "")
            ?? string.Empty;

        Track insert = new()
        {
            Id = trackAppends.Id,
            Name = trackAppends.Title,
            Date = trackAppends.Recording.FirstReleaseDate,
            DiscNumber = mediaFile.Parsed?.DiscNumber ?? 0,
            TrackNumber = mediaFile.Parsed?.TrackNumber ?? 0,

            Filename = "/" + StoragePathHelpers.GetName(path: mediaFile.Path.Replace(oldChar: '\\', newChar: '/')),
            Quality = (int)
                Math.Floor(
                    d: (
                        mediaFile.FFprobe?.Format.BitRate
                        ?? mediaFile.TagFile?.Properties?.AudioBitrate * 1000
                        ?? 0
                    ) / 1000.0
                ),
            Duration = HmsRegex()
                .Replace(
                    input: (
                        mediaFile.FFprobe?.Duration ?? mediaFile.TagFile!.Properties!.Duration
                    ).ToString(format: @"hh\:mm\:ss"),
                    replacement: ""
                ),

            FolderId = libraryFolder.Id,
            Folder = path.Replace(oldValue: ResolveLibraryRoot(libraryFolder: libraryFolder), newValue: "").Replace(oldValue: "\\", newValue: "/"),
            HostFolder = path.PathName(),

            Cover = releaseCoverPalette?.Url is not null
                ? $"/{releaseCoverPalette.Url.FileName()}"
                : null,
        };

        await recordingRepository.Store(recording: insert);

        await LinkToRelease(track: insert, releaseAppends: releaseAppends);
        await LinkToLibrary(track: insert, library: libraryFolder.FolderLibraries.FirstOrDefault()!.Library);
        await LinkToArtist(insert: insert, artistAppends: artistAppends);
        foreach (
            MusicBrainzGenreDetails musicBrainzGenreDetails in trackAppends.Genres
                ?? trackAppends.Recording.Genres
        )
        {
            MusicGenre musicGenre = new()
            {
                Id = musicBrainzGenreDetails.Id,
                Name = musicBrainzGenreDetails.Name,
            };
            await musicGenreRepository.Store(musicGenre: musicGenre);
        }
        List<MusicGenreTrack> genres = (trackAppends.Genres ?? trackAppends.Recording.Genres)
            .Select(selector: genre => new MusicGenreTrack { TrackId = insert.Id, GenreId = genre.Id })
            .ToList();

        if (genres.Count > 0)
            await musicGenreRepository.LinkToRecording(genreRecordings: genres);

        logger.LogTrace(message: "Recording {Title} stored", args: trackAppends.Title);
    }

    private async Task LinkToArtist(Track insert, MusicBrainzArtistAppends[] artistAppends)
    {
        foreach (MusicBrainzArtistAppends artist in artistAppends)
        {
            ArtistTrack link = new() { ArtistId = artist.Id, TrackId = insert.Id };
            await recordingRepository.LinkToArtist(insert: link);
        }
    }
}
