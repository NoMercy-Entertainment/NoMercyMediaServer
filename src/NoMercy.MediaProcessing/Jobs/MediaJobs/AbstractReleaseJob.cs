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

using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractReleaseJob : IShouldQueue
{
    public Guid Id { get; set; }
    public MediaFolderExtend MediaFolder { get; set; } = new();
    public Folder BaseFolder { get; set; } = new();
    public Ulid LibraryId { get; set; }

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void Dispose() { }
}
