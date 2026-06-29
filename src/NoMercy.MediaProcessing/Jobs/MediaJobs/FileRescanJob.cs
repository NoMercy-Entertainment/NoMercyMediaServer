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

using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

using Microsoft.Extensions.Logging;
using NoMercy.Storage;
namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class FileRescanJob : AbstractMediaJob
{
    public FileRescanJob() { }

    public FileRescanJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        ILoggerFactory loggerFactory
    )
        : base(storageFactory, storageDriver, loggerFactory) { }

    public override string QueueName => "file";
    public override int Priority => 10;

    public override async Task Handle()
    {
        Log.LogInformation("[FileRescanJob] Handle() entered for id={Id}, libraryId={LibraryId}", Id, LibraryId);

        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        LibraryRepository libraryRepository = new(context, StorageDriver);
        LibraryManager libraryManager = new(
            libraryRepository,
            jobDispatcher,
            context,
            StorageDriver,
            StorageFactory, LoggerFactory.CreateLogger<LibraryManager>()
        );

        Library? library = await libraryManager.RescanFiles(LibraryId, Id);

        string type = library?.Type switch
        {
            MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => "tv",
            MediaTypes.MovieMediaType => "movie",
            _ => "unknown",
        };

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshedEvent { QueryKey = [type, Id.ToString()] }
            );

            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshedEvent { QueryKey = ["libraries", LibraryId.ToString()] }
            );

            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshedEvent { QueryKey = ["home"] }
            );

            await EventBusProvider.Current.PublishAsync(
                new MediaFilesScannedEvent { MediaId = Id, LibraryId = LibraryId }
            );
        }
    }
}
