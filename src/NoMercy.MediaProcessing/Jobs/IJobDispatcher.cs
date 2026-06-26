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

using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs;

/// <summary>
/// Media-domain job dispatch surface. Extends the low-level queue
/// <see cref="NoMercyQueue.Core.Interfaces.IJobDispatcher"/> with the
/// strongly-typed DispatchJob overloads consumed by controllers and managers.
/// </summary>
public interface IJobDispatcher : NoMercyQueue.Core.Interfaces.IJobDispatcher
{
    void DispatchJob<TJob>(
            Ulid libraryId,
            Ulid folderId,
            string id,
            string inputFile,
            Ulid? sourceDriverId = null
        )
            where TJob : AbstractEncoderJob, new();

    void DispatchJob<TJob>(
            Ulid libraryId,
            Ulid folderId,
            Guid releaseId,
            string filePath
        )
            where TJob : AbstractMusicFolderJob, new();

    void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string filePath)
            where TJob : AbstractMusicFolderJob, new();

    void DispatchJob<TJob>(int id, Ulid libraryId)
            where TJob : AbstractMediaJob, new();

    void DispatchJob<TJob>(Ulid libraryId)
            where TJob : AbstractMediaJob, new();

    void DispatchJob<TJob>(int id, Library library, int? priority = null)
            where TJob : AbstractMediaJob, new();

    void DispatchJob<TJob>(string baseFolderPath, Ulid libraryId)
            where TJob : AbstractMusicFolderJob, new();

    void DispatchJob<TJob>(
            Ulid libraryId,
            Guid id,
            Folder baseFolder,
            MediaFolderExtend mediaFolder
        )
            where TJob : AbstractReleaseJob, new();

    void DispatchJob<TJob>(Guid id1, Guid? id2 = null, Guid? id3 = null)
            where TJob : AbstractFanArtDataJob, new();

    void DispatchJob<TJob>(MusicBrainzReleaseGroup musicBrainzReleaseGroup)
            where TJob : MusicMetadataJob, new();

    void DispatchJob<TJob>(MusicBrainzArtist musicBrainzArtist)
            where TJob : MusicMetadataJob, new();

    void DispatchJob<TJob>(
            Guid id,
            Ulid folderId,
            FolderMetadata folderMetaData,
            MediaFile mediaFile,
            MusicBrainzTrack foundTrack,
            Ulid libraryId,
            string inputFolder,
            string inputFile
        )
            where TJob : MusicEncodeJob, new();

    void DispatchJob<TJob>(Track track)
            where TJob : AbstractLyricJob, new();

    void DispatchColorPaletteJob(
            string entityType,
            string entityId,
            int? priority = null
        );

    void DispatchJob<TJob, TChild>(IEnumerable<TChild> data, string name)
        where TJob : AbstractShowExtraDataJob<TChild, string>, new();
}
