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
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMusicEncoderJob : IShouldQueue, IJobStorageInjector
{
    public Ulid LibraryId { get; set; }
    public Guid Id { get; set; }

    public Ulid FolderId { get; set; }

    /// <summary>
    /// MusicBrainz id of the release being imported. This, not the release itself,
    /// is what the payload carries: the graph is rebuilt from it in
    /// <see cref="Hydrate"/>, out of the provider cache the import already wrote.
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


    /// <summary>
    /// Rebuilds the release-derived state the payload no longer carries, from the
    /// release id it does carry.
    /// <para>
    /// The provider client answers this from its own on-disk cache — the same
    /// response the import already fetched and wrote — so this is a local read,
    /// not a MusicBrainz call, for every release that was imported. Keeping a
    /// second copy of that response in the queue database was storing the
    /// provider's cache twice.
    /// </para>
    /// <para>
    /// Returns false when the release cannot be rebuilt, which is not worth
    /// retrying: a job whose release no longer resolves is a job whose work is
    /// gone.
    /// </para>
    /// </summary>
    protected async Task<bool> Hydrate()
    {
        if (FolderMetaData is not null)
            return true;

        using MusicBrainzReleaseClient releaseClient = new();
        MusicBrainzReleaseAppends? release = await releaseClient.WithAllAppends(ReleaseId);
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
    }

    public void Dispose() { }
}
