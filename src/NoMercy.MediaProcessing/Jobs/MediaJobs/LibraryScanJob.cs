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

using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Encoder.Analysis;
using NoMercy.Events;
using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class LibraryScanJob : AbstractMediaJob
{
    public LibraryScanJob() { }

    public LibraryScanJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        ILoggerFactory loggerFactory,
        IMediaAnalyzer mediaAnalyzer,
        IFilenameParserPipeline filenameParser
    )
        : base(storageFactory, storageDriver, loggerFactory)
    {
        _mediaAnalyzer = mediaAnalyzer;
        _filenameParser = filenameParser;
    }

    // Private field, not a [JsonIgnore] public property — SerializationHelper.Populate
    // (Newtonsoft) only re-hydrates public members, so this survives the queue's
    // rebuild-then-populate cycle (see QueueWorker.ExecuteWithTransientRetry) untouched.
    private readonly IMediaAnalyzer _mediaAnalyzer = null!;
    private readonly IFilenameParserPipeline _filenameParser = null!;

    public override string QueueName => "library";
    public override int Priority => 10;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        IEventBus? eventBus = EventBusProvider.IsConfigured ? EventBusProvider.Current : null;

        LibraryRepository libraryRepository = new(context, StorageDriver);
        LibraryManager libraryManager = new(
            libraryRepository,
            jobDispatcher,
            context,
            StorageDriver,
            StorageFactory,
            _mediaAnalyzer,
            _filenameParser,
            LoggerFactory.CreateLogger<LibraryManager>(),
            eventBus
        );

        await libraryManager.ProcessNewLibraryItems(LibraryId);
    }
}
