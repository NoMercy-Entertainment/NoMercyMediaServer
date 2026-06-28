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
using Newtonsoft.Json;
using NoMercy.Providers.AcoustId;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMusicFolderJob : IShouldQueue, IJobStorageInjector
{
    public string InputFolder { get; set; } = string.Empty;
    public Ulid LibraryId { get; set; }
    public Ulid FolderId { get; set; }
    public Guid ReleaseId { get; set; }

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageDriver StorageDriver { get; set; } = null!;

    [JsonIgnore]
    public IAudioFingerprinter AudioFingerprinter { get; set; } = null!;

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        StorageDriver = serviceProvider.GetRequiredService<IStorageDriver>();
        AudioFingerprinter = serviceProvider.GetRequiredService<IAudioFingerprinter>();
    }

    public void Dispose() { }
}
