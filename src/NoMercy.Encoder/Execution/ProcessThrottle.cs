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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.Execution;

public class ProcessThrottle(ILogger<ProcessThrottle> logger, IProcessSuspender suspender)
{
    // Registered as a singleton in DI and called concurrently from every
    // worker thread that wants to suspend/resume its ffmpeg child. The
    // HashSet must be guarded — without the lock, two threads adding distinct
    // pids at the same time could corrupt internal buckets and a third caller
    // could see a phantom membership result.
    private readonly HashSet<int> _suspendedPids = [];
    private readonly Lock _lock = new();

    public void Suspend(int processId)
    {
        // Lock spans the OS call so a concurrent Resume on the same pid can't
        // interleave between set-add and the actual SIGSTOP, leaving the OS
        // state and the tracked state out of sync. Workers throttle per-pid
        // so contention is negligible.
        lock (_lock)
        {
            if (!_suspendedPids.Add(processId))
                return;

            suspender.Suspend(processId);

            logger.LogDebug("Suspended process {Pid}", processId);
        }
    }

    public void Resume(int processId)
    {
        lock (_lock)
        {
            if (!_suspendedPids.Remove(processId))
                return;

            suspender.Resume(processId);

            logger.LogDebug("Resumed process {Pid}", processId);
        }
    }

    public bool IsSuspended(int processId)
    {
        lock (_lock)
        {
            return _suspendedPids.Contains(processId);
        }
    }
}
