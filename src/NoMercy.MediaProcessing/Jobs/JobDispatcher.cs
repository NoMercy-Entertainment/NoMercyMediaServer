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

using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Images.Palettes;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using QueueJobDispatcher = NoMercyQueue.JobDispatcher;

namespace NoMercy.MediaProcessing.Jobs;

public class JobDispatcher : IJobDispatcher
{
    public JobDispatcher(QueueJobDispatcher dispatcher)
    {
        Dispatcher = dispatcher;
    }

    public JobDispatcher()
    {
        Dispatcher = QueueRunner.Current?.Dispatcher;
    }

    public void Dispatch(IShouldQueue job)
    {
        Dispatcher.Dispatch(job: job);
    }

    public virtual void Dispatch(IShouldQueue job, string onQueue, int priority)
    {
        Dispatcher.Dispatch(job: job, onQueue: onQueue, priority: priority);
    }

    public void DispatchChild(
        IShouldQueue job,
        string onQueue,
        int priority,
        int parentJobId,
        string groupTag
    )
    {
        Dispatcher.DispatchChild(job: job, onQueue: onQueue, priority: priority, parentJobId: parentJobId, groupTag: groupTag);
    }

    private QueueJobDispatcher Dispatcher =>
        field
        ?? throw new InvalidOperationException(
            message: "JobDispatcher requires a QueueRunner instance. Ensure QueueRunner is initialized before dispatching jobs."
        );

    public virtual void DispatchJob<TJob>(
        Ulid libraryId,
        Ulid folderId,
        string id,
        string inputFile,
        Ulid? sourceDriverId = null
    )
        where TJob : AbstractEncoderJob, new()
    {
        TJob job = new()
        {
            LibraryId = libraryId,
            FolderId = folderId,
            Id = id,
            InputFile = inputFile,
            SourceDriverId = sourceDriverId,
        };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(
        Ulid libraryId,
        Ulid folderId,
        Guid releaseId,
        string filePath
    )
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new()
        {
            LibraryId = libraryId,
            ReleaseId = releaseId,
            InputFolder = filePath,
        };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string filePath)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new()
        {
            LibraryId = libraryId,
            FolderId = folderId,
            InputFolder = filePath,
        };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(int id, Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = libraryId };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { LibraryId = libraryId };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(int id, Library library, int? priority = null)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = library.Id };
        Dispatcher.Dispatch(job: job, onQueue: job.QueueName, priority: priority ?? job.Priority);
    }

    internal virtual void DispatchJob<TJob, TChild>(TChild data)
        where TJob : AbstractMediaExraDataJob<TChild>, new()
    {
        TJob job = new() { Storage = data };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob, TChild>(IEnumerable<TChild> data, string name)
        where TJob : AbstractShowExtraDataJob<TChild, string>, new()
    {
        TJob job = new() { Storage = data, Name = name };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(string baseFolderPath, Ulid libraryId)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { InputFolder = baseFolderPath, LibraryId = libraryId };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(
        Ulid libraryId,
        Guid id,
        Folder baseFolder,
        MediaFolderExtend mediaFolder
    )
        where TJob : AbstractReleaseJob, new()
    {
        TJob job = new()
        {
            LibraryId = libraryId,
            Id = id,
            BaseFolder = baseFolder,
            MediaFolder = mediaFolder,
        };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(Guid id1, Guid? id2 = null, Guid? id3 = null)
        where TJob : AbstractFanArtDataJob, new()
    {
        TJob job = new()
        {
            Id1 = id1,
            Id2 = id2,
            Id3 = id3,
        };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(MusicBrainzReleaseGroup musicBrainzReleaseGroup)
        where TJob : MusicMetadataJob, new()
    {
        TJob job = new() { MusicBrainzReleaseGroup = musicBrainzReleaseGroup };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(MusicBrainzArtist musicBrainzArtist)
        where TJob : MusicMetadataJob, new()
    {
        TJob job = new() { MusicBrainzArtist = musicBrainzArtist };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(
        Guid id,
        Ulid folderId,
        FolderMetadata folderMetaData,
        MediaFile mediaFile,
        MusicBrainzTrack foundTrack,
        Ulid libraryId,
        string inputFolder,
        string inputFile
    )
        where TJob : MusicEncodeJob, new()
    {
        TJob job = new()
        {
            Id = id,
            FolderId = folderId,
            FoundTrack = foundTrack,
            FolderMetaData = folderMetaData,
            MediaFile = mediaFile,
            LibraryId = libraryId,
            InputFolder = inputFolder,
            InputFile = inputFile,
        };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchJob<TJob>(Track track)
        where TJob : AbstractLyricJob, new()
    {
        TJob job = new() { Track = track };
        Dispatcher.Dispatch(job: job);
    }

    public virtual void DispatchColorPaletteJob(
        string entityType,
        string entityId,
        int? priority = null
    )
    {
        if (!NeedsPalette(entityType: entityType, entityId: entityId))
            return;

        ColorPaletteJob job = new(entityType: entityType, entityId: entityId);
        Dispatch(job: job, onQueue: "palette", priority: priority ?? job.Priority);
    }

    /// <summary>
    /// True when the entity has no usable palette yet. Every caller dispatches one
    /// of these per cast member per stored title, and people are shared across
    /// titles, so the overwhelming majority target an entity that was painted long
    /// ago. <see cref="ColorPaletteJob"/> already early-returns in that case, which
    /// makes the queued job a guaranteed no-op that still costs a dedup query, an
    /// insert, a reserve, a deserialize and a delete. Measured on a real library:
    /// ~3.6k palette jobs inserted per minute against 128,612 of 130,865 people
    /// already painted — the queue grew ~1000x faster than it drained. Answering
    /// the same question here, before the row exists, keeps the queue for real work.
    /// <para>Failing open (returning true) is deliberate: a broken check must queue
    /// the job and let it decide, never silently drop a palette.</para>
    /// </summary>
    protected virtual bool NeedsPalette(string entityType, string entityId)
    {
        IPaletteSource? source = DefaultPaletteSourceRegistry.Instance.Resolve(entityType: entityType);
        if (source is null)
            return true;

        try
        {
            using MediaContext db = new();
            string? current = source
                .CurrentPaletteAsync(db: db, entityId: entityId, ct: CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            return string.IsNullOrEmpty(value: current);
        }
        catch
        {
            return true;
        }
    }
}
