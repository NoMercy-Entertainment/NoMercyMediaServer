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

// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue.MediaServer;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMusicEncoderJob
    : IShouldQueue,
        IJobStorageInjector,
        IJobWithSharedInput
{
    public Ulid LibraryId { get; set; }
    public Guid Id { get; set; }

    public Ulid FolderId { get; set; }

    /// <summary>
    /// MusicBrainz id of the release being imported. This, not the release itself,
    /// is what the payload carries — see <see cref="SharedInputKey"/>.
    /// </summary>
    public Guid ReleaseId { get; set; }

    /// <summary>
    /// Which track of the release this job encodes. The track is read back out of
    /// the hydrated release rather than serialized alongside it.
    /// </summary>
    public Guid TrackId { get; set; }

    public string BasePath { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public int Year { get; set; }

    /// <summary>
    /// Where the release graph lives, since every track of an album needs the same
    /// one. Serializing it per job wrote the same megabyte once per track.
    /// </summary>
    [JsonIgnore]
    public string? SharedInputKey => ReleaseId == Guid.Empty ? null : KeyFor(ReleaseId);

    public static string KeyFor(Guid releaseId) => SharedInputKeys.Release(releaseId);

    // Rebuilt by Hydrate before the job runs. Handle() and the storing that follows
    // it read these exactly as they always did.
    //
    // Read from a payload but never written back to one: rows queued before this
    // change still carry the release inline, and refusing to deserialize it would
    // turn every one of them into a job that cannot name its own work. They run
    // from what they already carry; only what this process writes is slim.
    public MusicBrainzTrack FoundTrack { get; set; } = null!;

    public bool ShouldSerializeFoundTrack() => false;

    public FolderMetadata FolderMetaData { get; set; } = null!;

    public bool ShouldSerializeFolderMetaData() => false;

    public MediaFile MediaFile { get; set; } = null!;

    public bool ShouldSerializeMediaFile() => false;

    [JsonIgnore]
    public IQueueJobBlobStore BlobStore { get; set; } = null!;

    /// <summary>
    /// Rebuilds the release-derived state the payload no longer carries.
    /// <para>
    /// Returns false when the release blob is gone, which is not a failure worth
    /// retrying: the shared input is swept only once no queued job references it,
    /// so a job that cannot find its release is a job whose release was already
    /// finished with.
    /// </para>
    /// </summary>
    protected async Task<bool> Hydrate()
    {
        if (FolderMetaData is not null)
            return true;

        string? stored = await BlobStore.ReadAsync(KeyFor(ReleaseId));
        if (stored is null)
            return false;

        MusicBrainzReleaseAppends? release =
            JsonConvert.DeserializeObject<MusicBrainzReleaseAppends>(stored);
        if (release is null)
            return false;

        MusicBrainzTrack? track = release
            .Media.SelectMany(media => media.Tracks)
            .FirstOrDefault(candidate => candidate.Id == TrackId);

        if (track is null)
            return false;

        FoundTrack = track;
        MediaFile = new() { Path = InputFile, Name = Path.GetFileName(InputFile) };
        FolderMetaData = new()
        {
            MusicBrainzRelease = release,
            BasePath = BasePath,
            ArtistName = ArtistName,
            ReleaseName = ReleaseName,
            Year = Year,
        };

        return true;
    }

    public string InputFolder { get; set; } = string.Empty;

    public string InputFile { get; set; } = string.Empty;

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageDriver StorageDriver { get; set; } = null!;

    [JsonIgnore]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [JsonIgnore]
    protected ILogger Log => field ??= LoggerFactory.CreateLogger(GetType());

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        StorageDriver = serviceProvider.GetRequiredService<IStorageDriver>();
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        BlobStore = serviceProvider.GetRequiredService<IQueueJobBlobStore>();
    }

    public void Dispose() { }
}
