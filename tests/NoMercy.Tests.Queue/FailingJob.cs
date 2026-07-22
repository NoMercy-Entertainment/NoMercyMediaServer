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

// ReSharper disable All

using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Tests.Queue;

public class FailingJob : IShouldQueue
{
    private readonly string _param1;

    public string QueueName => "default";
    public int Priority => 0;

    public FailingJob(string param1)
    {
        _param1 = param1;
    }

    public Task Handle()
    {
        throw new Exception(message: $"This job always fails. the argument was {_param1}");
    }
}
