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
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.MediaProcessing.ReleaseGroups;
using NoMercy.MediaProcessing.Releases;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using Microsoft.Extensions.Logging;
using NoMercy.Storage;
namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class AudioImportJob : AbstractMusicFolderJob
{
    public AudioImportJob() { }

    public AudioImportJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        IAudioFingerprinter audioFingerprinter,
        ILoggerFactory loggerFactory
    )
        : base(storageFactory: storageFactory, storageDriver: storageDriver, audioFingerprinter: audioFingerprinter, loggerFactory: loggerFactory) { }

    public override string QueueName => "import";
    public override int Priority => 6;

    private MediaFolderExtend? _rootFolder;

    private MediaContext? _mediaContext;

    public override async Task Handle()
    {
        if (InputFolder.Contains(value: "[Singles]"))
        {
            await ImportSingles();
        }
        else
        {
            await ImportRelease();
        }
    }

    // Identify a file with no embedded MusicBrainz id by acoustic fingerprint:
    // fingerprint via the injected IAudioFingerprinter, look it up against
    // AcoustId, and return the first MusicBrainz release id found (null on any
    // failure, so the caller skips the file rather than crashing the import).
    private async Task<Guid?> TryDiscoverReleaseIdAsync(MediaFile mediaFile)
    {
        try
        {
            using AcoustIdFingerprintClient client = new(fingerprinter: AudioFingerprinter);
            AcoustIdFingerprint? result = await client.Lookup(file: mediaFile.Path);
            if (result is null)
                return null;

            foreach (AcoustIdFingerprintResult fingerprintResult in result.Results)
            {
                foreach (
                    AcoustIdFingerprintRecording? recording in fingerprintResult.Recordings ?? []
                )
                {
                    Guid? releaseId = recording
                        ?.Releases?.FirstOrDefault(predicate: release => release.Id != Guid.Empty)
                        ?.Id;
                    if (releaseId is not null && releaseId != Guid.Empty)
                        return releaseId;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.LogInformation(message: "Fingerprint lookup failed for {Path}: {Message}", args: [mediaFile.Path, ex.Message]);
            return null;
        }
    }

    private async Task ImportSingles()
    {
        (
            MusicBrainzReleaseClient musicBrainzReleaseClient,
            MusicBrainzArtistClient musicBrainzArtistClient,
            MusicBrainzRecordingClient musicBrainzRecordingClient,
            ReleaseGroupManager releaseGroupManager,
            ReleaseManager releaseManager,
            ArtistManager artistManager,
            RecordingManager recordingManager,
            MusicGenreManager musicGenreManager,
            Library albumLibrary,
            Folder folderLibrary,
            Func<IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)>> audioFilesFactory,
            _,
            JobDispatcher jobDispatcher
        ) = Init();

        using (musicBrainzReleaseClient)
        using (musicBrainzArtistClient)
        using (musicBrainzRecordingClient)
        await using (_mediaContext)
        {
            bool wasEmpty = !await _mediaContext!.AlbumLibrary.AnyAsync(predicate: al =>
                al.LibraryId == LibraryId
            );

            Dictionary<
                Guid,
                (
                    MusicBrainzReleaseAppends SingleAppends,
                    List<(MediaFile MediaFile, AudioTagModel audioTagModel)> File
                )
            > processedSingles = new();
            await foreach ((MediaFile mediaFile, AudioTagModel audioTag) in audioFilesFactory())
            {
                if (
                    audioTag.MusicBrainz?.ReleaseId is null
                    || audioTag.MusicBrainz.ReleaseId == Guid.Empty
                )
                {
                    // No MusicBrainz id in the file tags — fall back to acoustic
                    // fingerprinting to identify the release. Skip the file only if
                    // fingerprinting also fails to find a match.
                    Guid? discoveredReleaseId = await TryDiscoverReleaseIdAsync(mediaFile: mediaFile);
                    if (discoveredReleaseId is null)
                        continue;

                    audioTag.MusicBrainz ??= new();
                    audioTag.MusicBrainz.ReleaseId = discoveredReleaseId.Value;
                }

                MusicBrainzReleaseAppends? releaseAppends =
                    await musicBrainzReleaseClient.WithAllAppends(id: audioTag.MusicBrainz.ReleaseId);
                if (releaseAppends is null)
                    continue;

                if (
                    processedSingles.TryGetValue(
                        key: audioTag.MusicBrainz.ReleaseId,
                        value: out (
                            MusicBrainzReleaseAppends SingleAppends,
                            List<(MediaFile MediaFile, AudioTagModel audioTagModel)> File
                        ) value
                    )
                )
                {
                    value.File.Add(item: (mediaFile, audioTag));
                }
                else
                {
                    processedSingles.Add(
                        key: audioTag.MusicBrainz.ReleaseId,
                        value: (releaseAppends, [(mediaFile, audioTag)])
                    );
                }
            }

            foreach (
                (
                    MusicBrainzReleaseAppends singleRelease,
                    List<(MediaFile mediaFile, AudioTagModel audioTagModel)> files
                ) in processedSingles.Values
            )
            {
                await AddSingleOrRelease(
                    release: singleRelease,
                    musicGenreManager: musicGenreManager,
                    releaseGroupManager: releaseGroupManager,
                    releaseManager: releaseManager,
                    albumLibrary: albumLibrary,
                    folderLibrary: folderLibrary,
                    audioFiles: files,
                    musicBrainzArtistClient: musicBrainzArtistClient,
                    artistManager: artistManager,
                    jobDispatcher: jobDispatcher,
                    musicBrainzRecordingClient: musicBrainzRecordingClient,
                    recordingManager: recordingManager
                );

                jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzReleaseGroup: singleRelease.MusicBrainzReleaseGroup);
                await SendRefresh(query: ["music", "start"]);
            }

            if (wasEmpty && processedSingles.Count > 0)
                await SendRefresh(query: ["libraries"]);
        }
        try
        {
            musicBrainzReleaseClient.Dispose();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        try
        {
            musicBrainzArtistClient.Dispose();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        try
        {
            musicBrainzRecordingClient.Dispose();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        try
        {
            if (_mediaContext != null)
                await _mediaContext.DisposeAsync();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        _mediaContext = null;
    }

    private async Task ImportRelease()
    {
        (
            MusicBrainzReleaseClient musicBrainzReleaseClient,
            MusicBrainzArtistClient musicBrainzArtistClient,
            MusicBrainzRecordingClient musicBrainzRecordingClient,
            ReleaseGroupManager releaseGroupManager,
            ReleaseManager releaseManager,
            ArtistManager artistManager,
            RecordingManager recordingManager,
            MusicGenreManager musicGenreManager,
            Library albumLibrary,
            Folder folderLibrary,
            Func<IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)>> audioFilesFactory,
            Dictionary<Guid, (MusicBrainzReleaseAppends ReleaseAppends, int Count)> releases,
            JobDispatcher jobDispatcher
        ) = Init();

        using (musicBrainzReleaseClient)
        using (musicBrainzArtistClient)
        using (musicBrainzRecordingClient)
        await using (_mediaContext)
        {
            bool wasEmpty = !await _mediaContext!.AlbumLibrary.AnyAsync(predicate: al =>
                al.LibraryId == LibraryId
            );

            // First pass: count releases without storing all tags in memory
            await foreach ((_, AudioTagModel audioTag) in audioFilesFactory())
            {
                if (
                    audioTag.MusicBrainz?.ReleaseId is null
                    || audioTag.MusicBrainz.ReleaseId == Guid.Empty
                )
                    continue;

                MusicBrainzReleaseAppends? releaseAppends =
                    await musicBrainzReleaseClient.WithAllAppends(id: audioTag.MusicBrainz.ReleaseId);
                if (releaseAppends is null)
                    continue;

                if (
                    releases.TryGetValue(
                        key: releaseAppends.Id,
                        value: out (MusicBrainzReleaseAppends ReleaseAppends, int Count) value
                    )
                )
                    releases[key: releaseAppends.Id] = (releaseAppends, value.Count + 1);
                else
                    releases.Add(key: releaseAppends.Id, value: (releaseAppends, 1));
            }

            // pick the most common release
            MusicBrainzReleaseAppends? release = releases
                .OrderByDescending(keySelector: x => x.Value.Count)
                .FirstOrDefault()
                .Value.ReleaseAppends;
            if (release is null)
            {
                await using MediaContext failureContext = new();
                await ImportFailureRecorder.RecordAsync(
                    context: failureContext,
                    jobType: "AudioImportJob",
                    filePath: InputFolder,
                    libraryId: LibraryId,
                    errorMessage: "MusicBrainz release could not be resolved for this folder."
                );
                return;
            }

            // Second pass: collect only files that match the chosen release
            List<(MediaFile MediaFile, AudioTagModel AudioTag)> matchingFiles = [];
            await foreach ((MediaFile mediaFile, AudioTagModel audioTag) in audioFilesFactory())
            {
                if (
                    audioTag.MusicBrainz?.ReleaseId == release.Id
                    || (
                        audioTag.MusicBrainz?.ReleaseTrackId != null
                        && release.Media.Any(predicate: m =>
                            m.Tracks.Any(predicate: t =>
                                t.Id == audioTag.MusicBrainz.ReleaseTrackId
                                || t.Id == audioTag.MusicBrainz.RecordingId
                                || t.Recording.Id == audioTag.MusicBrainz.RecordingId
                                || t.Recording.Id == audioTag.MusicBrainz.ReleaseTrackId
                            )
                        )
                    )
                )
                {
                    matchingFiles.Add(item: (mediaFile, audioTag));
                }
            }

            await AddSingleOrRelease(
                release: release,
                musicGenreManager: musicGenreManager,
                releaseGroupManager: releaseGroupManager,
                releaseManager: releaseManager,
                albumLibrary: albumLibrary,
                folderLibrary: folderLibrary,
                audioFiles: matchingFiles,
                musicBrainzArtistClient: musicBrainzArtistClient,
                artistManager: artistManager,
                jobDispatcher: jobDispatcher,
                musicBrainzRecordingClient: musicBrainzRecordingClient,
                recordingManager: recordingManager
            );

            jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzReleaseGroup: release.MusicBrainzReleaseGroup);
            await SendRefresh(query: ["music", "start"]);

            if (wasEmpty)
                await SendRefresh(query: ["libraries"]);
        }
        try
        {
            musicBrainzReleaseClient.Dispose();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        try
        {
            musicBrainzArtistClient.Dispose();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        try
        {
            musicBrainzRecordingClient.Dispose();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        try
        {
            if (_mediaContext != null)
                await _mediaContext.DisposeAsync();
        }
        catch (Exception disposeEx)
        {
            Log.LogError(message: "Dispose failed: {DisposeEx}", args: disposeEx);
        }
        _mediaContext = null;
    }

    private static async Task SendRefresh(object?[] query)
    {
        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = query }
            );
    }

    private async Task AddSingleOrRelease(
        MusicBrainzReleaseAppends release,
        MusicGenreManager musicGenreManager,
        ReleaseGroupManager releaseGroupManager,
        ReleaseManager releaseManager,
        Library albumLibrary,
        Folder folderLibrary,
        List<(MediaFile MediaFile, AudioTagModel AudioTag)> audioFiles,
        MusicBrainzArtistClient musicBrainzArtistClient,
        ArtistManager artistManager,
        JobDispatcher jobDispatcher,
        MusicBrainzRecordingClient musicBrainzRecordingClient,
        RecordingManager recordingManager
    )
    {
        CoverArtImageManagerManager.CoverPalette? coverPalette =
            await CoverArtImageManagerManager.Add(id: release.MusicBrainzReleaseGroup.Id, priority: true);
        if (coverPalette is not null)
        {
            using Image<Rgba32>? downloadedImage = await CoverArtCoverArtClient.Download(
                url: coverPalette.Url
            );
        }

        await AddGenres(genres: release.Genres, musicGenreManager: musicGenreManager);

        await releaseGroupManager.Store(releaseGroup: release.MusicBrainzReleaseGroup, id: LibraryId, coverPalette: coverPalette);
        await releaseManager.Store(
            releaseAppends: release,
            library: albumLibrary,
            libraryFolder: folderLibrary,
            mediaFile: audioFiles.First().MediaFile,
            coverPalette: coverPalette
        );

        foreach (ReleaseArtistCredit artistCredit in release.ArtistCredit)
        {
            MusicBrainzArtistAppends? artistDetails = await musicBrainzArtistClient.WithAllAppends(
                id: artistCredit.MusicBrainzArtist.Id
            );
            if (artistDetails is null)
                continue;
            await artistManager.Store(artistCredit: artistDetails, releaseAppends: release, library: albumLibrary, libraryFolder: folderLibrary);
            jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzArtist: artistDetails);
            await SendRefresh(query: ["music", "artist", artistDetails.Id]);
        }

        List<MusicBrainzTrack> allTracks = release.Media.SelectMany(selector: m => m.Tracks).ToList();

        for (int index = 0; index < allTracks.Count; index++)
        {
            MusicBrainzTrack musicBrainzTrack = allTracks[index: index];

            int idx = release
                .Media.ToList()
                .FindIndex(match: t => t.Tracks.All(predicate: w => w.Id == musicBrainzTrack.Id));

            MediaFile? mediaFile = null;
            AudioTagModel? audioTag = null;
            foreach ((MediaFile file, AudioTagModel tag) in audioFiles)
            {
                if (
                    (
                        tag.MusicBrainz?.ReleaseTrackId != musicBrainzTrack.Id
                        && tag.MusicBrainz?.ReleaseTrackId != musicBrainzTrack.Recording.Id
                        && tag.MusicBrainz?.RecordingId != musicBrainzTrack.Id
                        && tag.MusicBrainz?.RecordingId != musicBrainzTrack.Recording.Id
                    )
                    || (
                        !musicBrainzTrack.Title.ContainsSanitized(
                            value: tag.Tags?.Title ?? file.Parsed?.Title
                        )
                        && !(Math.Abs(value: tag.Duration - musicBrainzTrack.Duration) < 5)
                        && musicBrainzTrack.Position != tag.Tags?.Track
                        && musicBrainzTrack.Position != file.Parsed?.TrackNumber
                        && musicBrainzTrack.Position != index + 1
                        && musicBrainzTrack.Position * idx != index + 1
                    )
                )
                    continue;
                mediaFile = file;
                audioTag = tag;
                break;
            }
            if (mediaFile is null || audioTag is null)
                continue;

            MusicBrainzRecordingAppends? musicBrainzRecording =
                await musicBrainzRecordingClient.WithAllAppends(id: musicBrainzTrack.Recording.Id);
            if (musicBrainzRecording is null)
                continue;

            await AddGenres(genres: musicBrainzRecording.Genres, musicGenreManager: musicGenreManager);

            await recordingManager.Store(
                releaseAppends: release,
                trackAppends: musicBrainzTrack,
                artistAppends: [],
                mediaFile: mediaFile,
                libraryFolder: folderLibrary,
                releaseCoverPalette: coverPalette
            );

            foreach (MusicBrainzArtistCredit artistCredit in musicBrainzRecording.ArtistCredit)
            {
                if (_rootFolder is null)
                    continue;
                MusicBrainzArtistAppends? artistDetails =
                    await musicBrainzArtistClient.WithAllAppends(id: artistCredit.MusicBrainzArtist.Id);
                if (artistDetails is null)
                    continue;
                await artistManager.Store(
                    artistCredit: artistDetails,
                    library: albumLibrary,
                    libraryFolder: folderLibrary,
                    mediaFolder: _rootFolder!,
                    track: musicBrainzTrack
                );
                jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzArtist: artistDetails);
                await SendRefresh(query: ["music", "artist", artistDetails.Id]);
            }
        }

        await SendRefresh(query: ["music", "album", release.Id]);
    }

    private static async Task AddGenres(
        MusicBrainzGenreDetails[] genres,
        MusicGenreManager musicGenreManager
    )
    {
        foreach (MusicBrainzGenreDetails musicBrainzGenreDetails in genres)
            await musicGenreManager.Store(genre: musicBrainzGenreDetails);
    }

    private AudioImportContext Init()
    {
        _mediaContext = new();
        JobDispatcher jobDispatcher = new();
        MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        MusicBrainzArtistClient musicBrainzArtistClient = new();
        MusicBrainzRecordingClient musicBrainzRecordingClient = new();
        Dictionary<Guid, (MusicBrainzReleaseAppends ReleaseAppends, int Count)> releases = new();

        ReleaseGroupRepository releaseGroupRepository = new(context: _mediaContext);
        ReleaseGroupManager releaseGroupManager = new(releaseGroupRepository: releaseGroupRepository, jobDispatcher: jobDispatcher, logger: LoggerFactory.CreateLogger<ReleaseGroupManager>());

        MusicGenreRepository musicGenreRepository = new(context: _mediaContext);
        MusicGenreManager musicGenreManager = new(musicGenreRepository: musicGenreRepository);

        ReleaseRepository releaseRepository = new(context: _mediaContext);
        ReleaseManager releaseManager = new(
            releaseRepository: releaseRepository,
            musicGenreRepository: musicGenreRepository,
            storageFactory: StorageFactory,
            jobDispatcher: jobDispatcher, logger: LoggerFactory.CreateLogger<ReleaseManager>()
        );

        ArtistRepository artistRepository = new(context: _mediaContext);
        ArtistManager artistManager = new(
            artistRepository: artistRepository,
            musicGenreRepository: musicGenreRepository,
            jobDispatcher: jobDispatcher,
            storageFactory: StorageFactory, logger: LoggerFactory.CreateLogger<ArtistManager>()
        );

        RecordingRepository recordingRepository = new(context: _mediaContext);
        RecordingManager recordingManager = new(
            recordingRepository: recordingRepository,
            musicGenreRepository: musicGenreRepository,
            artistRepository: artistRepository,
            storageDriver: StorageDriver,
            storageFactory: StorageFactory, logger: LoggerFactory.CreateLogger<RecordingManager>()
        );

        Library albumLibrary = _mediaContext
            .Libraries.Where(predicate: f => f.Id == LibraryId)
            .Include(navigationPropertyPath: f => f.FolderLibraries)
                .ThenInclude(navigationPropertyPath: f => f.Folder)
            .First();
        Folder folderLibrary = albumLibrary.FolderLibraries.First().Folder;
        Func<IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)>> audioFilesFactory =
            GetAudioFiles;

        return new(
            MusicBrainzReleaseClient: musicBrainzReleaseClient,
            MusicBrainzArtistClient: musicBrainzArtistClient,
            MusicBrainzRecordingClient: musicBrainzRecordingClient,
            ReleaseGroupManager: releaseGroupManager,
            ReleaseManager: releaseManager,
            ArtistManager: artistManager,
            RecordingManager: recordingManager,
            MusicGenreManager: musicGenreManager,
            AlbumLibrary: albumLibrary,
            FolderLibrary: folderLibrary,
            AudioFilesFactory: audioFilesFactory,
            Releases: releases,
            JobDispatcher: jobDispatcher
        );
    }

    private async IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)> GetAudioFiles()
    {
        await using MediaScan mediaScan = new(driver: StorageDriver);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .DisableRegexFilter()
            .EnableFileListing()
            .Process(rootFolder: InputFolder, depth: 1);

        _rootFolder ??= rootFolders.FirstOrDefault();

        IEnumerable<MediaFile> files = rootFolders.SelectMany(selector: mediaFolderExtend =>
            mediaFolderExtend.Files ?? Enumerable.Empty<MediaFile>()
        );

        ConcurrentBag<(MediaFile, AudioTagModel)> bag = [];

        foreach (MediaFile mediaFile in files)
        {
            AudioTagModel audioTagModel = await AudioTagModel.Create(fileItem: mediaFile);
            bag.Add(item: (mediaFile, audioTagModel));
        }

        foreach ((MediaFile mediaFile, AudioTagModel audioTagModel) in bag)
            yield return (mediaFile, audioTagModel);
    }
}

public record AudioImportContext(
    MusicBrainzReleaseClient MusicBrainzReleaseClient,
    MusicBrainzArtistClient MusicBrainzArtistClient,
    MusicBrainzRecordingClient MusicBrainzRecordingClient,
    ReleaseGroupManager ReleaseGroupManager,
    ReleaseManager ReleaseManager,
    ArtistManager ArtistManager,
    RecordingManager RecordingManager,
    MusicGenreManager MusicGenreManager,
    Library AlbumLibrary,
    Folder FolderLibrary,
    Func<IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)>> AudioFilesFactory,
    Dictionary<Guid, (MusicBrainzReleaseAppends ReleaseAppends, int Count)> Releases,
    JobDispatcher JobDispatcher
);
