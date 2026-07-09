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

using NoMercy.NmSystem.Monitoring;
using NoMercyQueue;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Bridges <see cref="MediaActivityMonitor"/> (the media-server's playback
/// signal) into the generic <see cref="IWorkerActivityGate"/> the queue
/// library consults. Only queues that read heavily from the same NAS mount
/// media segments stream from are deferred; the encoder queue and
/// network-only queues (e.g. music/metadata) are never deferred.
/// </summary>
public class MediaPlaybackActivityGate(MediaActivityMonitor monitor) : IWorkerActivityGate
{
    private const int DeferIntervalSeconds = 2;

    private static readonly TimeSpan DeferIntervalValue = TimeSpan.FromSeconds(
        DeferIntervalSeconds
    );

    private static readonly HashSet<string> NasReadHeavyQueues = new(StringComparer.Ordinal)
    {
        "library",
        "file",
        "import",
        "extras",
    };

    public bool ShouldDefer(string queueName)
    {
        return monitor.IsActive && NasReadHeavyQueues.Contains(queueName);
    }

    public TimeSpan DeferInterval => DeferIntervalValue;
}
