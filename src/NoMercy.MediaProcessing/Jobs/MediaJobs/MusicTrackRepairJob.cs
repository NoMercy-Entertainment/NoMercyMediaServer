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
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Re-derives which physical file each track of one already-imported release
/// actually IS, and corrects the stored rows in place.
/// <para>
/// <see cref="AudioImportJob.AddSingleOrRelease"/> used to let several different
/// MusicBrainz tracks all claim the same physical file (fixed in
/// <see cref="AudioImportJob.ResolveFilesForTracks"/>), so a release imported before
/// that fix has its <c>Tracks</c> rows still pointing several different track ids at
/// one file each. This job re-scans the release's folder, re-runs the corrected
/// matcher, and re-stores every resolved (track, file) pair — an update to the
/// SAME track ids <see cref="RecordingManager.Store"/> already owns, not a new
/// import. It deliberately never dispatches an encode: the files this walks are the
/// library's own source files, already playable, and the bug was in which title got
/// attached to which of them — not in the audio itself.
/// </para>
/// </summary>
public class MusicTrackRepairJob : AbstractMusicFolderJob
{
    public MusicTrackRepairJob() { }

    public MusicTrackRepairJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        IAudioFingerprinter audioFingerprinter,
        ILoggerFactory loggerFactory
    )
        : base(storageFactory, storageDriver, audioFingerprinter, loggerFactory) { }

    public override string QueueName => "import";
    public override int Priority => 6;

    public override async Task Handle()
    {
        await using MediaContext mediaContext = new();

        using MusicBrainzReleaseClient musicBrainzReleaseClient = new(ReleaseId);
        MusicBrainzReleaseAppends? release;
        try
        {
            release = await musicBrainzReleaseClient.WithAllAppends();
        }
        catch (HttpRequestException ex)
        {
            // MusicBrainz rate-limits aggressively, and a repair batch can dispatch
            // hundreds of these at once. A 429/503 here is transient — the row is
            // left exactly as it was (safe, just still unrepaired), and re-running
            // the repair endpoint later picks it back up since it is idempotent.
            Log.LogWarning(
                "MusicTrackRepairJob: MusicBrainz lookup failed for release {ReleaseId} ({InputFolder}): {Message}",
                ReleaseId,
                InputFolder,
                ex.Message
            );
            return;
        }

        if (release is null)
        {
            Log.LogWarning(
                "MusicTrackRepairJob: MusicBrainz release {ReleaseId} could not be resolved for {InputFolder}",
                ReleaseId,
                InputFolder
            );
            return;
        }

        Library albumLibrary = await mediaContext
            .Libraries.Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
                .ThenInclude(f => f.Folder)
            .FirstAsync();
        Folder folderLibrary = albumLibrary.FolderLibraries.First().Folder;

        List<(MediaFile MediaFile, AudioTagModel AudioTag)> audioFiles = [];
        await foreach ((MediaFile mediaFile, AudioTagModel audioTag) in GetAudioFiles())
            audioFiles.Add((mediaFile, audioTag));

        if (audioFiles.Count == 0)
        {
            Log.LogWarning(
                "MusicTrackRepairJob: no audio files found under {InputFolder}; nothing to repair",
                InputFolder
            );
            return;
        }

        List<MusicBrainzTrack> allTracks = release.Media.SelectMany(m => m.Tracks).ToList();
        Dictionary<Guid, MediaFile> resolvedFileByTrackId = AudioImportJob.ResolveFilesForTracks(
            allTracks,
            audioFiles
        );

        RecordingRepository recordingRepository = new(mediaContext);
        MusicGenreRepository musicGenreRepository = new(mediaContext);
        ArtistRepository artistRepository = new(mediaContext);
        RecordingManager recordingManager = new(
            recordingRepository,
            musicGenreRepository,
            artistRepository,
            StorageDriver,
            StorageFactory,
            LoggerFactory.CreateLogger<RecordingManager>()
        );

        int repaired = 0;
        foreach (MusicBrainzTrack track in allTracks)
        {
            if (!resolvedFileByTrackId.TryGetValue(track.Id, out MediaFile? mediaFile))
                continue;

            await recordingManager.Store(release, track, [], mediaFile, folderLibrary, null);
            repaired++;
        }

        Log.LogInformation(
            "MusicTrackRepairJob: re-resolved {Repaired}/{Total} track(s) for {Title} under {InputFolder}",
            repaired,
            allTracks.Count,
            release.Title,
            InputFolder
        );
    }

    private async IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)> GetAudioFiles()
    {
        await using MediaScan mediaScan = new(StorageDriver);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .DisableRegexFilter()
            .EnableFileListing()
            .Process(InputFolder, 2);

        IEnumerable<MediaFile> files = rootFolders.SelectMany(mediaFolderExtend =>
            mediaFolderExtend.Files ?? Enumerable.Empty<MediaFile>()
        );

        foreach (MediaFile mediaFile in files)
            yield return (mediaFile, await AudioTagModel.Create(mediaFile));
    }
}
