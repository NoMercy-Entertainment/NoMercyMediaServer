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
using NoMercy.Data.Jobs;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FFProbe;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;
using NoMercyQueue;

namespace NoMercy.Data.Services;

public partial class MusicLogic : IAsyncDisposable
{
    private readonly IDbContextFactory<MediaContext> _mediaContextFactory;
    private readonly IStorageFactory _storageFactory;
    private readonly IAudioFingerprinter _audioFingerprinter;
    private ConcurrentBag<MediaFile>? Files { get; set; }
    private MediaFolderExtend ListPath { get; set; }

    private int Year { get; set; }
    private string AlbumName { get; set; }
    private string ArtistName { get; set; }
    private Library Library { get; set; }
    private Folder? Folder { get; set; }

    private readonly ILogger<MusicLogic> _logger;

    public MusicLogic(
        ILogger<MusicLogic> logger,
        Library library,
        MediaFolderExtend listPath,
        IDbContextFactory<MediaContext> mediaContextFactory,
        IStorageFactory storageFactory,
        IAudioFingerprinter audioFingerprinter
    )
    {
        _logger = logger;
        _mediaContextFactory = mediaContextFactory;
        _storageFactory = storageFactory;
        _audioFingerprinter = audioFingerprinter;
        Library = library;
        ListPath = listPath;

        Files = listPath.Files;

        Match match = PathRegex().Match(input: listPath.Path);
        ArtistName = match.Groups[groupname: "artist"].Success ? match.Groups[groupname: "artist"].Value : string.Empty;
        AlbumName = match.Groups[groupname: "album"].Success ? match.Groups[groupname: "album"].Value : string.Empty;
        Year = match.Groups[groupname: "year"].Success ? Convert.ToInt32(value: match.Groups[groupname: "year"].Value) : 1970;

        // Post-migration: Folder.Path = "" (sub-path inside driver root). Match
        // by resolving each folder's driver root and comparing against the scan path.
        Folder = Library
            .FolderLibraries.Select(selector: folderLibrary => folderLibrary.Folder)
            .FirstOrDefault(predicate: folder =>
            {
                IStorage folderStorage = _storageFactory.For(
                    folderId: folder.Id,
                    driverId: folder.DriverId,
                    subPath: string.Empty
                );
                // Resolve through the driver, not the IStorage facade: the facade's
                // GetFullPath is a LocalStorage-only escape hatch that throws on every
                // remote backend, so a facade call here killed folder matching for
                // NFS / SMB / S3 / WebDAV music libraries.
                string driverRoot = folderStorage.Driver.GetFullPath(path: folder.Path);
                return listPath.Path.StartsWith(value: driverRoot, comparisonType: StringComparison.OrdinalIgnoreCase);
            });

        _logger.LogTrace(message: "Files");
        _logger.LogTrace(message: "{Files}", args: Files ?? []);

        _logger.LogTrace(message: "ArtistName: {ArtistName}", args: ArtistName);
        _logger.LogTrace(message: "AlbumName {AlbumName}", args: AlbumName);
        _logger.LogTrace(message: "Year: {Year}", args: Year);

        _logger.LogTrace(message: "Folder: {Path}", args: Folder?.Path);
    }

    public async Task Process()
    {
        _logger.LogTrace(message: "Processing Folder: {Path}", args: Folder?.Path);
        await Parallel.ForEachAsync(
            source: Files ?? [],
            parallelOptions: SystemParallelism.Options,
            body: async (file, cancellationToken) =>
            {
                await using MediaContext mediaContext =
                    await _mediaContextFactory.CreateDbContextAsync(cancellationToken: cancellationToken);

                try
                {
                    _logger.LogDebug(message: "Analyzing File: {Name}", args: file.Name);
                    FfProbeData ffProbeData = await FfProbe.CreateAsync(
                        file: file.Path,
                        ct: cancellationToken
                    );

                    AcoustIdFingerprintRecording? fingerPrintRecording = await MatchTrack(
                        file: file,
                        ffProbeData: ffProbeData
                    );
                    if (fingerPrintRecording is not null)
                    {
                        foreach (
                            AcoustIdFingerprintReleaseGroups release in fingerPrintRecording.Releases
                                ?? []
                        )
                        {
                            if (release.TrackCount == null || release.TrackCount != Files?.Count)
                            {
                                _logger.LogTrace(message: "Track Count Mismatch: {Title}", args: release.Title);
                                return;
                            }

                            try
                            {
                                await ProcessRelease(mediaContext: mediaContext, release: release, mediaFile: file);
                            }
                            catch (Exception e)
                            {
                                if (e.Message.Contains(value: "404"))
                                    return;
                                _logger.LogError(message: e.Message);
                            }
                        }

                        return;
                    }

                    AcoustIdFingerprintReleaseGroups? fallbackParsedResult = FallbackParser(
                        file: file,
                        ffProbeData: ffProbeData
                    );
                    if (fallbackParsedResult is null)
                        return;

                    await ProcessRelease(mediaContext: mediaContext, release: fallbackParsedResult, mediaFile: file);
                }
                catch (Exception e)
                {
                    if (e.Message.Contains(value: "404"))
                        return;
                    _logger.LogError(message: e.Message);
                }
            }
        );
    }

    private async Task ProcessRelease(
        MediaContext mediaContext,
        AcoustIdFingerprintReleaseGroups release,
        MediaFile mediaFile
    )
    {
        _logger.LogTrace(message: "Processing release: {Title} with id: {Id}", args: [release.Title, release.Id]);

        using MusicBrainzReleaseClient musicBrainzReleaseClient = new(id: release.Id);

        MusicBrainzReleaseAppends? releaseAppends = await musicBrainzReleaseClient.WithAllAppends();

        if (releaseAppends is null || string.IsNullOrEmpty(value: releaseAppends.Title))
        {
            _logger.LogWarning(message: "Release not found: {Title}", args: release.Title);
            await Task.CompletedTask;
            return;
        }

        if (await StoreReleaseGroups(mediaContext: mediaContext, musicBrainzRelease: releaseAppends) is null)
            _logger.LogTrace(
                message: "Release Group already exists: {Title}",
                args: releaseAppends.MusicBrainzReleaseGroup.Title
            );
        // await Task.CompletedTask;
        // return;
        else
            _logger.LogDebug(
                message: "Processing release: {Title} with id: {Id}", args: [release.Title, release.Id]
            );

        if (await StoreRelease(mediaContext: mediaContext, musicBrainzRelease: releaseAppends, mediaFile: mediaFile) is null)
            _logger.LogTrace(message: "Release already exists: {Title}", args: releaseAppends.Title);
        // await Task.CompletedTask;
        // return;
        await LinkReleaseToReleaseGroup(mediaContext: mediaContext, musicBrainzRelease: releaseAppends);
        await LinkReleaseToLibrary(mediaContext: mediaContext, musicBrainzRelease: releaseAppends);

        foreach (MusicBrainzMedia media in releaseAppends.Media)
        foreach (MusicBrainzTrack track in media.Tracks)
        {
            if (await StoreTrack(mediaContext: mediaContext, musicBrainzRelease: releaseAppends, musicBrainzTrack: track, musicBrainzMedia: media, mediaFile: mediaFile) is null)
                continue;

            await LinkTrackToRelease(mediaContext: mediaContext, track: track, release: releaseAppends);

            foreach (ReleaseArtistCredit artist in track.ArtistCredit)
            {
                await StoreArtist(mediaContext: mediaContext, musicBrainzArtist: artist.MusicBrainzArtist);
                await LinkArtistToTrack(mediaContext: mediaContext, musicBrainzArtistMusicBrainzArtist: artist.MusicBrainzArtist, musicBrainzTrack: track);

                await LinkArtistToAlbum(mediaContext: mediaContext, musicBrainzArtistMusicBrainzArtist: artist.MusicBrainzArtist, musicBrainzRelease: releaseAppends);
                await LinkArtistToLibrary(mediaContext: mediaContext, musicBrainzArtistMusicBrainzArtist: artist.MusicBrainzArtist);

                await LinkArtistToReleaseGroup(
                    mediaContext: mediaContext,
                    musicBrainzRelease: releaseAppends,
                    artistId: artist.MusicBrainzArtist.Id
                );
            }
        }

        await Task.CompletedTask;
    }

    private async Task<AcoustIdFingerprintRecording?> MatchTrack(
        MediaFile file,
        FfProbeData ffProbeData
    )
    {
        _logger.LogTrace(message: "Matching Track: {Name}", args: file.Name);

        AcoustIdFingerprint? lookupResult;
        try
        {
            AcoustIdFingerprintClient acoustIdFingerprintClient = new(fingerprinter: _audioFingerprinter);
            lookupResult = await acoustIdFingerprintClient.Lookup(file: file.Path);
            acoustIdFingerprintClient.Dispose();
            if (lookupResult is null)
                return null;
        }
        catch (Exception e)
        {
            _logger.LogError(message: e.Message);
            throw;
        }

        AcoustIdFingerprintRecording? fingerPrintRecording = null;

        foreach (AcoustIdFingerprintResult fingerPrint in lookupResult.Results ?? [])
        {
            _logger.LogTrace(message: "Matching Recording: {Id}", args: fingerPrint.Id);
            foreach (AcoustIdFingerprintRecording? recording in fingerPrint.Recordings ?? [])
            {
                if (recording?.Releases is null)
                    continue;

                fingerPrintRecording = MatchRelease(file: file, recording: recording, ffProbeData: ffProbeData);

                if (fingerPrintRecording is not null)
                    break;

                fingerPrintRecording = MatchRelease(file: file, recording: recording, ffProbeData: ffProbeData, strictMatch: false);
            }
        }

        return fingerPrintRecording;
    }

    private AcoustIdFingerprintReleaseGroups? FallbackParser(
        MediaFile file,
        FfProbeData ffProbeData
    )
    {
        _logger.LogTrace(message: "Fallback Parser: {Name}", args: file.Name);
        string? albumId = ffProbeData
            .Format.Tags?.FirstOrDefault(predicate: t => t.Key == "MusicBrainz Album Id")
            .Value;

        _logger.LogTrace(message: "AlbumId: {AlbumId}", args: albumId);

        if (albumId is null)
            return null;

        return new() { Id = albumId.ToGuid() };
    }

    private AcoustIdFingerprintRecording? MatchRelease(
        MediaFile file,
        AcoustIdFingerprintRecording? recording,
        FfProbeData ffProbeData,
        bool strictMatch = true
    )
    {
        _logger.LogTrace(message: "Matching Release: {Title}", args: recording?.Title);
        if (recording is null)
            return null;

        AcoustIdFingerprintRecording? fingerPrintRecording = null;

        foreach (AcoustIdFingerprintReleaseGroups release in recording.Releases ?? [])
        {
            bool matchesTrackCount =
                release.TrackCount != null && release.TrackCount == Files?.Count;
            if (!matchesTrackCount)
                continue;

            string fileNameSanitized =
                file.Parsed?.Title?.RemoveDiacritics().RemoveNonAlphaNumericCharacters()
                ?? string.Empty;
            string recordNameSanitized = (
                recording.Title?.RemoveDiacritics().RemoveNonAlphaNumericCharacters()
            ).OrEmpty();
            bool matchesName =
                !fileNameSanitized.Equals(value: string.Empty)
                && !recordNameSanitized.Equals(value: string.Empty)
                && fileNameSanitized.Contains(value: recordNameSanitized);

            // var ffProbeData = FFProbe.AnalyseAsync(file.Path).Result;
            double fileDuration = ffProbeData.Format.Duration.TotalSeconds;
            int recordDuration = recording.Duration;
            bool matchesDuration =
                fileDuration > 0
                && recordDuration > 0
                && Math.Abs(value: recordDuration - fileDuration) < 10;

            if (strictMatch && matchesName && matchesDuration)
            {
                fingerPrintRecording = recording;
                break;
            }

            if (!matchesName && !matchesDuration)
                continue;
            fingerPrintRecording = recording;
            break;
        }

        return fingerPrintRecording;
    }

    private static string MakeArtistFolder(string artist)
    {
        string artistName = artist.RemoveDiacritics();

        string artistFolder = char.IsNumber(c: artistName[index: 0])
            ? "#"
            : artistName[index: 0].ToString().ToUpper();

        return $"/{artistFolder}/{artistName}";
    }

    private async Task<MusicBrainzReleaseAppends?> StoreReleaseGroups(
        MediaContext mediaContext,
        MusicBrainzReleaseAppends musicBrainzRelease
    )
    {
        _logger.LogTrace(
            message: "Storing Release Group: {Title}",
            args: musicBrainzRelease.MusicBrainzReleaseGroup.Title
        );

        bool hasReleaseGroup = mediaContext
            .ReleaseGroups.AsNoTracking()
            .Any(predicate: r => r.Id == musicBrainzRelease.MusicBrainzReleaseGroup.Id);

        if (hasReleaseGroup)
            return null;

        ReleaseGroup insert = new()
        {
            Id = musicBrainzRelease.MusicBrainzReleaseGroup.Id,
            Title = musicBrainzRelease.MusicBrainzReleaseGroup.Title,
            Description = string.IsNullOrEmpty(
                value: musicBrainzRelease.MusicBrainzReleaseGroup.Disambiguation
            )
                ? null
                : musicBrainzRelease.MusicBrainzReleaseGroup.Disambiguation,
            Year = musicBrainzRelease.MusicBrainzReleaseGroup.FirstReleaseDate.ParseYear(),
            LibraryId = Library.Id,
        };

        try
        {
            await mediaContext
                .ReleaseGroups.Upsert(entity: insert)
                .On(match: e => new { e.Id })
                .WhenMatched(
                    updater: (s, i) =>
                        new()
                        {
                            Id = i.Id,
                            Title = i.Title,
                            Description = i.Description,
                            Year = i.Year,
                            LibraryId = i.LibraryId,
                        }
                )
                .RunAsync();

            foreach (
                MusicBrainzGenreDetails genre in musicBrainzRelease.MusicBrainzReleaseGroup.Genres
                    ?? []
            )
                await LinkGenreToReleaseGroup(
                    mediaContext: mediaContext,
                    musicBrainzReleaseGroup: musicBrainzRelease.MusicBrainzReleaseGroup,
                    musicBrainzGenre: genre
                );

            MusicMetadataJob musicDescriptionJob = new(musicBrainzReleaseGroup: musicBrainzRelease.MusicBrainzReleaseGroup);
            QueueRunner.Current!.Dispatcher.Dispatch(job: musicDescriptionJob);
        }
        catch (Exception e)
        {
            _logger.LogError(message: e.Message);
            return null;
        }

        _logger.LogTrace(
            message: "Release Group stored: {Title}",
            args: musicBrainzRelease.MusicBrainzReleaseGroup.Title
        );
        return musicBrainzRelease;
    }

    private async Task<MusicBrainzReleaseAppends?> StoreRelease(
        MediaContext mediaContext,
        MusicBrainzReleaseAppends musicBrainzRelease,
        MediaFile mediaFile
    )
    {
        _logger.LogTrace(message: "Storing Release: {Title}", args: musicBrainzRelease.Title);
        MusicBrainzMedia? media = musicBrainzRelease.Media.FirstOrDefault(predicate: m => m.Tracks.Length > 0);
        if (media is null)
            return null;

        bool hasAlbum = mediaContext
            .Albums.AsNoTracking()
            .Any(predicate: a => a.Id == musicBrainzRelease.Id && a.Cover != null);

        if (hasAlbum)
            return musicBrainzRelease;

        string folder =
            mediaFile
                .Parsed?.FilePath.Replace(oldValue: "/" + mediaFile.Name, newValue: "")
                .Replace(oldValue: "\\" + mediaFile.Name, newValue: "")
            ?? string.Empty;

        Album insert = new()
        {
            Id = musicBrainzRelease.Id,
            Name = musicBrainzRelease.Title,
            TitleSort = musicBrainzRelease.Title.TitleSort(),
            Country = musicBrainzRelease.Country,
            Disambiguation = string.IsNullOrEmpty(value: musicBrainzRelease.Disambiguation)
                ? null
                : musicBrainzRelease.Disambiguation,
            Year =
                musicBrainzRelease.DateTime?.ParseYear()
                ?? musicBrainzRelease.ReleaseEvents?.FirstOrDefault()?.DateTime?.ParseYear()
                ?? 0,
            Tracks = media.Tracks.Length,

            LibraryId = Library.Id,
            FolderId = Folder!.Id,
            Folder = folder.Replace(oldValue: ResolveLibraryRoot(), newValue: "").Replace(oldValue: "\\", newValue: "/"),
            HostFolder = folder.PathName(),
        };

        try
        {
            await mediaContext
                .Albums.Upsert(entity: insert)
                .On(match: e => new { e.Id })
                .WhenMatched(
                    updater: (s, i) =>
                        new()
                        {
                            Id = i.Id,
                            Name = i.Name,
                            TitleSort = i.TitleSort,
                            Disambiguation = i.Disambiguation,
                            Description = i.Description,
                            Year = i.Year,
                            Country = i.Country,
                            Tracks = i.Tracks,
                            LibraryId = i.LibraryId,
                            Folder = i.Folder,
                            FolderId = i.FolderId,
                            HostFolder = i.HostFolder,
                        }
                )
                .RunAsync();

            foreach (MusicBrainzGenreDetails genre in musicBrainzRelease.Genres)
                await LinkGenreToRelease(mediaContext: mediaContext, artist: musicBrainzRelease, musicBrainzGenre: genre);

            CoverArtImageJob coverArtImageJob = new(musicBrainzRelease: musicBrainzRelease);
            QueueRunner.Current!.Dispatcher.Dispatch(job: coverArtImageJob);

            FanArtImagesJob fanartImagesJob = new(musicBrainzRelease: musicBrainzRelease);
            QueueRunner.Current!.Dispatcher.Dispatch(job: fanartImagesJob);

            if (EventBusProvider.IsConfigured)
                await EventBusProvider.Current.PublishAsync(
                    @event: new LibraryRefreshedEvent
                    {
                        QueryKey = ["music", "album", musicBrainzRelease.Id.ToString()],
                    }
                );
        }
        catch (Exception e)
        {
            _logger.LogError(message: e.Message);
            return null;
        }

        _logger.LogTrace(message: "Release stored: {Title}", args: musicBrainzRelease.Title);

        return musicBrainzRelease;
    }

    private async Task StoreArtist(
        MediaContext mediaContext,
        MusicBrainzArtistDetails musicBrainzArtist
    )
    {
        _logger.LogTrace(message: "Processing Artist: {Name}", args: musicBrainzArtist.Name);

        bool hasArtist = mediaContext.Artists.AsNoTracking().Any(predicate: a => a.Id == musicBrainzArtist.Id);

        if (hasArtist)
            return;

        string artistFolder = MakeArtistFolder(artist: musicBrainzArtist.Name);
        Artist insert = new()
        {
            Id = musicBrainzArtist.Id,
            Name = musicBrainzArtist.Name,
            Disambiguation = string.IsNullOrEmpty(value: musicBrainzArtist.Disambiguation)
                ? null
                : musicBrainzArtist.Disambiguation,
            Country = musicBrainzArtist.Country,
            // Use the same display-order sort title as TMDB titles: strip a leading
            // article and keep first-name-first. MusicBrainz's SortName inverts people
            // to surname-first ("Belle, Tony"), which files them under the wrong letter
            // in the A-Z index versus the name shown on the card.
            TitleSort = musicBrainzArtist.Name.TitleSort(),

            Folder = artistFolder,
            HostFolder = Path.Join(path1: ResolveLibraryRoot(), path2: artistFolder).PathName(),
            LibraryId = Library.Id,
            FolderId = Folder!.Id,
        };

        try
        {
            await mediaContext
                .Artists.Upsert(entity: insert)
                .On(match: e => new { e.Id })
                .WhenMatched(
                    updater: (s, i) =>
                        new()
                        {
                            Id = i.Id,
                            Name = i.Name,
                            TitleSort = i.TitleSort,
                            Disambiguation = i.Disambiguation,
                            Description = i.Description,

                            Folder = i.Folder,
                            HostFolder = i.HostFolder,
                            LibraryId = i.LibraryId,
                            FolderId = i.FolderId,
                        }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(message: e.Message);
            return;
        }

        try
        {
            foreach (MusicBrainzGenreDetails genre in musicBrainzArtist.Genres)
                await LinkGenreToArtist(mediaContext: mediaContext, musicBrainzArtist: musicBrainzArtist, musicBrainzGenre: genre);
        }
        catch (Exception e)
        {
            _logger.LogError(message: e.Message);
        }

        MusicMetadataJob musicDescriptionJob = new() { MusicBrainzArtist = musicBrainzArtist };
        QueueRunner.Current!.Dispatcher.Dispatch(job: musicDescriptionJob);

        FanArtImagesJob fanartImagesJob = new(musicBrainzArtist: musicBrainzArtist);
        QueueRunner.Current!.Dispatcher.Dispatch(job: fanartImagesJob);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent
                {
                    QueryKey = ["music", "artist", musicBrainzArtist.Id.ToString()],
                }
            );
    }

    private async Task<MusicBrainzTrack?> StoreTrack(
        MediaContext mediaContext,
        MusicBrainzReleaseAppends musicBrainzRelease,
        MusicBrainzTrack musicBrainzTrack,
        MusicBrainzMedia musicBrainzMedia,
        MediaFile mediaFile
    )
    {
        _logger.LogTrace(message: "Processing Track: {Title}", args: musicBrainzTrack.Title);

        bool hasTrack = mediaContext
            .Tracks.AsNoTracking()
            .Any(predicate: t => t.Id == musicBrainzTrack.Id && t.Filename != null && t.Duration != null);

        if (hasTrack)
            return null;

        Track insert = new()
        {
            Id = musicBrainzTrack.Id,
            Name = musicBrainzTrack.Title,
            Date =
                musicBrainzRelease.DateTime
                ?? musicBrainzRelease.ReleaseEvents?.FirstOrDefault()?.DateTime,
            DiscNumber = musicBrainzMedia.Position,
            TrackNumber = musicBrainzTrack.Position,
        };

        string? file = FileMatch(musicBrainzRelease: musicBrainzRelease, musicBrainzMedia: musicBrainzMedia, track: insert);

        if (file is not null)
        {
            _logger.LogTrace(message: "File Match: {File}", args: file);
            FfProbeData ffProbeData = await FfProbe.CreateAsync(file: file);
            string folder =
                mediaFile
                    .Parsed?.FilePath.Replace(oldValue: "/" + mediaFile.Name, newValue: "")
                    .Replace(oldValue: "\\" + mediaFile.Name, newValue: "")
                ?? string.Empty;

            insert.Filename = "/" + StoragePathHelpers.GetName(path: file.Replace(oldChar: '\\', newChar: '/'));
            insert.Quality = (int)Math.Floor(d: ffProbeData.Format.BitRate / 1000.0);
            insert.Duration = HmsRegex().Replace(input: ffProbeData.Duration.ToString(format: @"hh\:mm\:ss"), replacement: "");

            insert.FolderId = Folder!.Id;
            insert.Folder = folder.Replace(oldValue: ResolveLibraryRoot(), newValue: "").Replace(oldValue: "\\", newValue: "/");
            insert.HostFolder = folder.PathName();
        }

        try
        {
            await mediaContext
                .Tracks.Upsert(entity: insert)
                .On(match: e => new { e.Id })
                .WhenMatched(
                    updater: (ts, ti) =>
                        new()
                        {
                            Id = ti.Id,
                            Name = ti.Name,
                            DiscNumber = ti.DiscNumber,
                            TrackNumber = ti.TrackNumber,
                            Date = ti.Date,

                            Folder = string.IsNullOrEmpty(file) ? ts.Folder : ti.Folder,
                            FolderId = string.IsNullOrEmpty(file) ? ts.FolderId : ti.FolderId,
                            HostFolder = string.IsNullOrEmpty(file) ? ts.HostFolder : ti.HostFolder,
                            Duration = string.IsNullOrEmpty(file) ? ts.Duration : ti.Duration,
                            Filename = string.IsNullOrEmpty(file) ? ts.Filename : ti.Filename,
                            Quality = string.IsNullOrEmpty(file) ? ts.Quality : ti.Quality,
                        }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(message: e.Message);
            return null;
        }

        try
        {
            foreach (MusicBrainzGenreDetails genre in musicBrainzTrack.Genres ?? [])
                await LinkGenreToTrack(mediaContext: mediaContext, musicBrainzTrack: musicBrainzTrack, musicBrainzGenre: genre);
        }
        catch (Exception e)
        {
            _logger.LogError(message: e.Message);
            return null;
        }

        _logger.LogTrace(message: "Track stored: {Title}", args: musicBrainzTrack.Title);
        return musicBrainzTrack;
    }

    private string? FileMatch(
        MusicBrainzReleaseAppends musicBrainzRelease,
        MusicBrainzMedia musicBrainzMedia,
        Track track
    )
    {
        string? file = FindTrackWithAlbumNumberByNumberPadded(
            musicBrainzMedia: musicBrainzMedia,
            file: null,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: track.TrackNumber,
            padding: 4
        );
        file = FindTrackWithAlbumNumberByNumberPadded(
            musicBrainzMedia: musicBrainzMedia,
            file: file,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: track.TrackNumber,
            padding: 3
        );
        file = FindTrackWithAlbumNumberByNumberPadded(
            musicBrainzMedia: musicBrainzMedia,
            file: file,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: track.TrackNumber
        );

        file = FindTrackWithoutAlbumNumberByNumberPadded(
            musicBrainzMedia: musicBrainzMedia,
            file: file,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: track.TrackNumber,
            padding: 4
        );
        file = FindTrackWithoutAlbumNumberByNumberPadded(
            musicBrainzMedia: musicBrainzMedia,
            file: file,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: track.TrackNumber,
            padding: 3
        );
        file = FindTrackWithoutAlbumNumberByNumberPadded(
            musicBrainzMedia: musicBrainzMedia,
            file: file,
            numberOfAlbums: musicBrainzRelease.Media.Length,
            trackNumber: track.TrackNumber
        );

        return file;
    }

    private async Task LinkReleaseToReleaseGroup(
        MediaContext mediaContext,
        MusicBrainzReleaseAppends musicBrainzRelease
    )
    {
        _logger.LogTrace(
            message: "Linking Release to Release Group: {Title}",
            args: musicBrainzRelease.MusicBrainzReleaseGroup.Title
        );
        AlbumReleaseGroup insert = new()
        {
            AlbumId = musicBrainzRelease.Id,
            ReleaseGroupId = musicBrainzRelease.MusicBrainzReleaseGroup.Id,
        };

        await mediaContext
            .AlbumReleaseGroup.Upsert(entity: insert)
            .On(match: e => new { e.AlbumId, e.ReleaseGroupId })
            .WhenMatched(updater: (s, i) => new() { AlbumId = i.AlbumId, ReleaseGroupId = i.ReleaseGroupId })
            .RunAsync();
    }

    private async Task LinkArtistToReleaseGroup(
        MediaContext mediaContext,
        MusicBrainzReleaseAppends musicBrainzRelease,
        Guid artistId
    )
    {
        _logger.LogTrace(
            message: "Linking Artist to Release Group: {Title}",
            args: musicBrainzRelease.MusicBrainzReleaseGroup.Title
        );
        ArtistReleaseGroup insert = new()
        {
            ArtistId = artistId,
            ReleaseGroupId = musicBrainzRelease.MusicBrainzReleaseGroup.Id,
        };

        await mediaContext
            .ArtistReleaseGroup.Upsert(entity: insert)
            .On(match: e => new { e.ArtistId, e.ReleaseGroupId })
            .WhenMatched(
                updater: (s, i) => new() { ArtistId = i.ArtistId, ReleaseGroupId = i.ReleaseGroupId }
            )
            .RunAsync();
    }

    private async Task LinkReleaseToLibrary(
        MediaContext mediaContext,
        MusicBrainzReleaseAppends musicBrainzRelease
    )
    {
        _logger.LogTrace(message: "Linking Release to Library: {Title}", args: musicBrainzRelease.Title);
        AlbumLibrary insert = new() { AlbumId = musicBrainzRelease.Id, LibraryId = Library.Id };

        await mediaContext
            .AlbumLibrary.Upsert(entity: insert)
            .On(match: e => new { e.AlbumId, e.LibraryId })
            .WhenMatched(updater: (s, i) => new() { AlbumId = i.AlbumId, LibraryId = i.LibraryId })
            .RunAsync();
    }

    private async Task LinkArtistToLibrary(
        MediaContext mediaContext,
        MusicBrainzArtist musicBrainzArtistMusicBrainzArtist
    )
    {
        _logger.LogTrace(
            message: "Linking Artist to Library: {Name}",
            args: musicBrainzArtistMusicBrainzArtist.Name
        );
        ArtistLibrary insert = new()
        {
            ArtistId = musicBrainzArtistMusicBrainzArtist.Id,
            LibraryId = Library.Id,
        };

        await mediaContext
            .ArtistLibrary.Upsert(entity: insert)
            .On(match: e => new { e.ArtistId, e.LibraryId })
            .WhenMatched(updater: (s, i) => new() { ArtistId = i.ArtistId, LibraryId = i.LibraryId })
            .RunAsync();
    }

    private async Task LinkTrackToRelease(
        MediaContext mediaContext,
        MusicBrainzTrack? track,
        MusicBrainzReleaseAppends? release
    )
    {
        _logger.LogTrace(message: "Linking Track to Release: {Title}", args: track?.Title);
        if (track == null || release == null)
            return;

        AlbumTrack insert = new() { AlbumId = release.Id, TrackId = track.Id };

        await mediaContext
            .AlbumTrack.Upsert(entity: insert)
            .On(match: e => new { e.AlbumId, e.TrackId })
            .WhenMatched(updater: (s, i) => new() { AlbumId = i.AlbumId, TrackId = i.TrackId })
            .RunAsync();
    }

    private async Task LinkArtistToAlbum(
        MediaContext mediaContext,
        MusicBrainzArtist musicBrainzArtistMusicBrainzArtist,
        MusicBrainzReleaseAppends musicBrainzRelease
    )
    {
        _logger.LogTrace(message: "Linking Artist to Album: {Title}", args: musicBrainzRelease.Title);
        AlbumArtist insert = new()
        {
            AlbumId = musicBrainzRelease.Id,
            ArtistId = musicBrainzArtistMusicBrainzArtist.Id,
        };

        await mediaContext
            .AlbumArtist.Upsert(entity: insert)
            .On(match: e => new { e.AlbumId, e.ArtistId })
            .WhenMatched(updater: (s, i) => new() { AlbumId = i.AlbumId, ArtistId = i.ArtistId })
            .RunAsync();
    }

    private async Task LinkArtistToTrack(
        MediaContext mediaContext,
        MusicBrainzArtist musicBrainzArtistMusicBrainzArtist,
        MusicBrainzTrack musicBrainzTrack
    )
    {
        _logger.LogTrace(message: "Linking Artist to Track: {Title}", args: musicBrainzTrack.Title);
        ArtistTrack insert = new()
        {
            ArtistId = musicBrainzArtistMusicBrainzArtist.Id,
            TrackId = musicBrainzTrack.Id,
        };

        await mediaContext
            .ArtistTrack.Upsert(entity: insert)
            .On(match: e => new { e.ArtistId, e.TrackId })
            .WhenMatched(updater: (s, i) => new() { ArtistId = i.ArtistId, TrackId = i.TrackId })
            .RunAsync();
    }

    private async Task LinkGenreToReleaseGroup(
        MediaContext mediaContext,
        MusicBrainzReleaseGroup musicBrainzReleaseGroup,
        MusicBrainzGenreDetails musicBrainzGenre
    )
    {
        _logger.LogTrace(message: "Linking Genre to Release Group: {Title}", args: musicBrainzReleaseGroup.Title);
        MusicGenreReleaseGroup insert = new()
        {
            GenreId = musicBrainzGenre.Id,
            ReleaseGroupId = musicBrainzReleaseGroup.Id,
        };

        await mediaContext
            .MusicGenreReleaseGroup.Upsert(entity: insert)
            .On(match: e => new { e.GenreId, e.ReleaseGroupId })
            .WhenMatched(updater: (s, i) => new() { GenreId = i.GenreId, ReleaseGroupId = i.ReleaseGroupId })
            .RunAsync();
    }

    private async Task LinkGenreToArtist(
        MediaContext mediaContext,
        MusicBrainzArtistDetails musicBrainzArtist,
        MusicBrainzGenreDetails musicBrainzGenre
    )
    {
        _logger.LogTrace(message: "Linking Genre to Artist: {Name}", args: musicBrainzArtist.Name);

        bool genreExists = mediaContext
            .MusicGenres.AsNoTracking()
            .Any(predicate: g => g.Id == musicBrainzGenre.Id);

        if (!genreExists)
        {
            _logger.LogTrace(message: "Genre does not exist: {Name}, creating it", args: musicBrainzGenre.Name);
            MusicGenre genreInsert = new()
            {
                Id = musicBrainzGenre.Id,
                Name = musicBrainzGenre.Name,
            };

            await mediaContext
                .MusicGenres.Upsert(entity: genreInsert)
                .On(match: e => new { e.Id })
                .WhenMatched(updater: (s, i) => new() { Id = i.Id, Name = i.Name })
                .RunAsync();
        }

        ArtistMusicGenre insert = new()
        {
            MusicGenreId = musicBrainzGenre.Id,
            ArtistId = musicBrainzArtist.Id,
        };

        await mediaContext
            .ArtistMusicGenre.Upsert(entity: insert)
            .On(match: e => new { e.MusicGenreId, e.ArtistId })
            .WhenMatched(updater: (s, i) => new() { MusicGenreId = i.MusicGenreId, ArtistId = i.ArtistId })
            .RunAsync();
    }

    private async Task LinkGenreToRelease(
        MediaContext mediaContext,
        MusicBrainzReleaseAppends artist,
        MusicBrainzGenreDetails musicBrainzGenre
    )
    {
        _logger.LogTrace(message: "Linking Genre to Album: {Title}", args: artist.Title);
        AlbumMusicGenre insert = new() { MusicGenreId = musicBrainzGenre.Id, AlbumId = artist.Id };

        await mediaContext
            .AlbumMusicGenre.Upsert(entity: insert)
            .On(match: e => new { e.MusicGenreId, e.AlbumId })
            .WhenMatched(updater: (s, i) => new() { MusicGenreId = i.MusicGenreId, AlbumId = i.AlbumId })
            .RunAsync();
    }

    private async Task LinkGenreToTrack(
        MediaContext mediaContext,
        MusicBrainzTrack musicBrainzTrack,
        MusicBrainzGenreDetails musicBrainzGenre
    )
    {
        _logger.LogTrace(message: "Linking Genre to Track: {Title}", args: musicBrainzTrack.Title);
        MusicGenreTrack insert = new()
        {
            GenreId = musicBrainzGenre.Id,
            TrackId = musicBrainzTrack.Id,
        };

        await mediaContext
            .MusicGenreTrack.Upsert(entity: insert)
            .On(match: e => new { e.GenreId, e.TrackId })
            .WhenMatched(updater: (s, i) => new() { GenreId = i.GenreId, TrackId = i.TrackId })
            .RunAsync();
    }

    private string? FindTrackWithoutAlbumNumberByNumberPadded(
        MusicBrainzMedia musicBrainzMedia,
        string? file,
        int numberOfAlbums,
        int trackNumber,
        int padding = 2
    )
    {
        if (file is not null)
            return file;
        if (numberOfAlbums > 1)
            return file;

        return Files
            ?.FirstOrDefault(predicate: f =>
            {
                string fileName = Path.GetFileName(path: f.Parsed!.FilePath)
                    .RemoveDiacritics()
                    .RemoveNonAlphaNumericCharacters()
                    .ToLower();

                string matchNumber = $"{trackNumber.ToString().PadLeft(totalWidth: padding, paddingChar: '0')} ";
                string matchString = musicBrainzMedia
                    .Tracks[trackNumber - 1]
                    .Title.RemoveDiacritics()
                    .RemoveNonAlphaNumericCharacters()
                    .ToLower()
                    .Replace(oldValue: ".mp3", newValue: "");

                return fileName.StartsWith(value: matchNumber) && fileName.Contains(value: matchString);
            })
            ?.Parsed!.FilePath;
    }

    private string? FindTrackWithAlbumNumberByNumberPadded(
        MusicBrainzMedia musicBrainzMedia,
        string? file,
        int numberOfAlbums,
        int trackNumber,
        int padding = 2
    )
    {
        if (file is not null)
            return file;
        if (numberOfAlbums == 1)
            return file;

        return Files
            ?.FirstOrDefault(predicate: f =>
            {
                string fileName = Path.GetFileName(path: f.Parsed!.FilePath)
                    .RemoveDiacritics()
                    .RemoveNonAlphaNumericCharacters()
                    .ToLower();

                string matchNumber =
                    $"{musicBrainzMedia.Position}-{trackNumber.ToString().PadLeft(totalWidth: padding, paddingChar: '0')} ";
                string matchString = musicBrainzMedia
                    .Tracks[trackNumber - 1]
                    .Title.RemoveDiacritics()
                    .RemoveNonAlphaNumericCharacters()
                    .ToLower()
                    .Replace(oldValue: ".mp3", newValue: "");

                return fileName.StartsWith(value: matchNumber) && fileName.Contains(value: matchString);
            })
            ?.Parsed!.FilePath;
    }

    [GeneratedRegex(pattern: "^00:")]
    private static partial Regex HmsRegex();

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private string ResolveLibraryRoot()
    {
        if (Folder is null)
            return string.Empty;
        IStorage folderStorage = _storageFactory.For(folderId: Folder.Id, driverId: Folder.DriverId, subPath: string.Empty);
        // Resolve through the driver, not the IStorage facade: the facade's
        // GetFullPath is a LocalStorage-only escape hatch that throws on every
        // remote backend, so a facade call here killed folder-path resolution
        // for NFS / SMB / S3 / WebDAV music libraries.
        return folderStorage.Driver.GetFullPath(path: Folder.Path);
    }

    [GeneratedRegex(
        pattern: @"(?<library_folder>.+?)[\\\/]((?<letter>.{1})?|\[(?<type>.+?)\])[\\\/](?<artist>.+?)?[\\\/]?(\[(?<year>\d{4})\]?\s?(?<album>.*)?)"
    )]
    private static partial Regex PathRegex();
}
