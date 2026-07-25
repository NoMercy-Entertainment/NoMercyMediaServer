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
            "Storing Recording: {Title} - {Position}-{Position2} {Title2}", [releaseAppends.Title, musicBrainzMedia.Position, musicBrainzTrack.Position, musicBrainzTrack.Title]
        );

        MediaScan mediaScan = new(storageDriver);
        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType("music")
            .Process(mediaFolder.Path, 1);

        foreach (MediaFolderExtend folder in folders)
        {
            if (folder.Files is null || folder.Files.IsEmpty)
                continue;
            foreach (MediaFile file in folder.Files)
            {
                MediaFile? mediaFile = FileMatch(
                    file,
                    releaseAppends,
                    musicBrainzMedia,
                    musicBrainzTrack.Position
                );
                if (mediaFile is null)
                    continue;

                TagFile? tagFile = file.TagFile;
                if (tagFile == null || mediaFile.FFprobe == null)
                {
                    logger.LogError("File not found: {Name}", file.Name);
                    continue;
                }

                logger.LogTrace("Recording {Title} found", musicBrainzTrack.Title);

                string path =
                    mediaFile
                        .Parsed?.FilePath.Replace("/" + mediaFile.Name, "")
                        .Replace("\\" + mediaFile.Name, "")
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

                    Filename = "/" + StoragePathHelpers.GetName(mediaFile.Path.Replace('\\', '/')),
                    Quality = (int)
                        Math.Floor(
                            (
                                (double?)mediaFile.FFprobe?.Format.BitRate
                                ?? (double?)(tagFile!.Properties?.AudioBitrate * 1000)
                                ?? 0.0
                            ) / 1000.0
                        ),
                    Duration = HmsRegex()
                        .Replace(
                            (
                                mediaFile.FFprobe?.Duration
                                ?? tagFile!.Properties?.Duration
                                ?? TimeSpan.Zero
                            ).ToString(@"hh\:mm\:ss"),
                            ""
                        ),

                    FolderId = libraryFolder.Id,
                    Folder = path.Replace(ResolveLibraryRoot(libraryFolder), "").Replace("\\", "/"),
                    HostFolder = path.PathName(),

                    Cover = releaseCoverPalette?.Url is not null
                        ? $"/{releaseCoverPalette.Url.FileName()}"
                        : null,
                };

                await recordingRepository.Store(insert);

                await LinkToRelease(musicBrainzTrack, releaseAppends);
                await LinkToLibrary(
                    musicBrainzTrack,
                    libraryFolder.FolderLibraries.FirstOrDefault()!.Library
                );

                List<MusicGenreTrack> genres =
                    musicBrainzTrack
                        .Genres?.Select(genre => new MusicGenreTrack
                        {
                            TrackId = musicBrainzTrack.Id,
                            GenreId = genre.Id,
                        })
                        .ToList()
                    ?? [];

                await musicGenreRepository.LinkToRecording(genres);

                new JobDispatcher().DispatchColorPaletteJob("track", insert.Id.ToString());

                logger.LogTrace("Recording {Title} stored", musicBrainzTrack.Title);

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
            "Linking Recording to Artist: {Title} - {Title2}", [musicBrainzTrack.Title, releaseAppends.MusicBrainzReleaseGroup.Title]
        );

        foreach (ReleaseArtistCredit credit in releaseAppends.ArtistCredit)
        {
            ArtistTrack insert = new()
            {
                ArtistId = credit.MusicBrainzArtist.Id,
                TrackId = musicBrainzTrack.Id,
            };

            await recordingRepository.LinkToArtist(insert);
        }
    }

    private async Task LinkToRelease(Track track, MusicBrainzReleaseAppends releaseAppends)
    {
        logger.LogTrace("Linking Recording to Release: {Title}", releaseAppends.Title);

        AlbumTrack insert = new() { AlbumId = releaseAppends.Id, TrackId = track.Id };

        await recordingRepository.LinkToRelease(insert);
    }

    private async Task LinkToLibrary(MusicBrainzTrack track, Library library)
    {
        logger.LogTrace("Linking Recording to Library: {Title}", track.Title);

        LibraryTrack insert = new() { LibraryId = library.Id, TrackId = track.Id };

        await recordingRepository.LinkToLibrary(insert);
    }

    private async Task LinkToLibrary(Track track, Library library)
    {
        logger.LogTrace("Linking Recording to Library: {Title}", library.Title);

        LibraryTrack insert = new() { LibraryId = library.Id, TrackId = track.Id };

        await recordingRepository.LinkToLibrary(insert);
    }

    private MediaFile? FileMatch(
        MediaFile inputFile,
        MusicBrainzReleaseAppends musicBrainzRelease,
        MusicBrainzMedia musicBrainzMedia,
        int trackNumber
    )
    {
        bool hasMatch = FindTrackWithAlbumNumberByNumberPadded(
            inputFile,
            musicBrainzMedia,
            false,
            musicBrainzRelease.Media.Length,
            trackNumber,
            4
        );
        hasMatch = FindTrackWithAlbumNumberByNumberPadded(
            inputFile,
            musicBrainzMedia,
            hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber,
            3
        );
        hasMatch = FindTrackWithAlbumNumberByNumberPadded(
            inputFile,
            musicBrainzMedia,
            hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber
        );

        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(
            inputFile,
            musicBrainzMedia,
            hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber,
            4
        );
        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(
            inputFile,
            musicBrainzMedia,
            hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber,
            3
        );
        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(
            inputFile,
            musicBrainzMedia,
            hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber
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

        string fileName = Path.GetFileName(inputFile.Parsed.FilePath)
            .RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower();

        string matchNumber = $"{trackNumber.ToString().PadLeft(padding, '0')} ";
        if (musicBrainzMedia.Tracks.Length < trackNumber)
            return false;
        string matchString = musicBrainzMedia
            .Tracks[trackNumber - 1]
            .Title.RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower()
            .Replace(".mp3", "");

        return fileName.StartsWith(matchNumber) && fileName.Contains(matchString);
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

        string fileName = Path.GetFileName(inputFile.Parsed.FilePath)
            .RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower();

        string matchNumber =
            $"{musicBrainzMedia.Position}-{trackNumber.ToString().PadLeft(padding, '0')} ";
        if (musicBrainzMedia.Tracks.Length < trackNumber)
            return false;
        string matchString = musicBrainzMedia
            .Tracks[trackNumber - 1]
            .Title.RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower()
            .Replace(".mp3", "");

        return fileName.StartsWith(matchNumber) && fileName.Contains(matchString);
    }

    private string ResolveLibraryRoot(Folder libraryFolder)
    {
        IStorage folderStorage = storageFactory.For(
            libraryFolder.Id,
            libraryFolder.DriverId,
            string.Empty
        );
        return FolderRootPath(folderStorage, libraryFolder.Path);
    }

    [GeneratedRegex("^00:")]
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
        logger.LogTrace("Recording {Title} found", releaseAppends.Title);

        foreach (MusicBrainzArtistAppends artist in artistAppends)
        {
            try
            {
                CoverArtImageManagerManager.CoverPalette? coverPalette =
                    await FanArtImageManager.Add(artist.Id, true);

                if (coverPalette is not null)
                {
                    using Image<Rgba32>? downloadedImage = await FanArtImageClient.Download(
                        coverPalette.Url!
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
                        .Parsed?.FilePath.Replace("/" + mediaFile.Name, "")
                        .Replace("\\" + mediaFile.Name, "")
                        .Replace(ResolveLibraryRoot(libraryFolder), "")
                        .Replace("\\", "/"),
                    HostFolder = mediaFile
                        .Parsed?.FilePath.Replace("/" + mediaFile.Name, "")
                        .Replace("\\" + mediaFile.Name, "")
                        .PathName()!,

                    LibraryId = libraryFolder.FolderLibraries.FirstOrDefault()!.LibraryId,
                };

                await artistRepository.StoreAsync(artistEntity);
                jobDispatcher.DispatchJob<MusicMetadataJob>(artist);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to store recording artist metadata");
            }
        }

        string path =
            mediaFile
                .Parsed?.FilePath.Replace("/" + mediaFile.Name, "")
                .Replace("\\" + mediaFile.Name, "")
            ?? string.Empty;

        Track insert = new()
        {
            Id = trackAppends.Id,
            Name = trackAppends.Title,
            Date = trackAppends.Recording.FirstReleaseDate,
            DiscNumber = mediaFile.Parsed?.DiscNumber ?? 0,
            TrackNumber = mediaFile.Parsed?.TrackNumber ?? 0,

            Filename = "/" + StoragePathHelpers.GetName(mediaFile.Path.Replace('\\', '/')),
            Quality = (int)
                Math.Floor(
                    (
                        mediaFile.FFprobe?.Format.BitRate
                        ?? mediaFile.TagFile?.Properties?.AudioBitrate * 1000
                        ?? 0
                    ) / 1000.0
                ),
            Duration = HmsRegex()
                .Replace(
                    (
                        mediaFile.FFprobe?.Duration ?? mediaFile.TagFile!.Properties!.Duration
                    ).ToString(@"hh\:mm\:ss"),
                    ""
                ),

            FolderId = libraryFolder.Id,
            Folder = path.Replace(ResolveLibraryRoot(libraryFolder), "").Replace("\\", "/"),
            HostFolder = path.PathName(),

            Cover = releaseCoverPalette?.Url is not null
                ? $"/{releaseCoverPalette.Url.FileName()}"
                : null,
        };

        await recordingRepository.Store(insert);

        await LinkToRelease(insert, releaseAppends);
        await LinkToLibrary(insert, libraryFolder.FolderLibraries.FirstOrDefault()!.Library);
        await LinkToArtist(insert, artistAppends);
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
            await musicGenreRepository.Store(musicGenre);
        }
        List<MusicGenreTrack> genres = (trackAppends.Genres ?? trackAppends.Recording.Genres)
            .Select(genre => new MusicGenreTrack { TrackId = insert.Id, GenreId = genre.Id })
            .ToList();

        if (genres.Count > 0)
            await musicGenreRepository.LinkToRecording(genres);

        logger.LogTrace("Recording {Title} stored", trackAppends.Title);
    }

    private async Task LinkToArtist(Track insert, MusicBrainzArtistAppends[] artistAppends)
    {
        foreach (MusicBrainzArtistAppends artist in artistAppends)
        {
            ArtistTrack link = new() { ArtistId = artist.Id, TrackId = insert.Id };
            await recordingRepository.LinkToArtist(link);
        }
    }
}
