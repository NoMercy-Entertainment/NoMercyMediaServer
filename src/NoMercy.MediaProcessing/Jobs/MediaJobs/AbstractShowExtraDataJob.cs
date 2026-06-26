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

using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractShowExtraDataJob<T, TS> : IShouldQueue
{
    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public TS Name { get; set; } = default!;
    private T[]? _storage;

    public IEnumerable<T> Storage
    {
        get => _storage ??= [];
        set => _storage = value.ToArray();
    }

    public abstract Task Handle();

    public void Dispose()
    {
        _storage = null;
    }
}
