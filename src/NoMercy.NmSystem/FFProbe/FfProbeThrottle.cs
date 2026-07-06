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

namespace NoMercy.NmSystem.FFProbe;

/// <summary>
/// Global throttle for ffprobe process instances to prevent spawning too many
/// concurrent ffprobe processes, which can make the system unresponsive.
/// Scales with CPU cores: min 2, max ProcessorCount (capped at 16).
/// </summary>
public static class FfProbeThrottle
{
    public static int MaxConcurrentProbes { get; } =
        Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

    // Remote probes stream the file header over the network (NFS/SMB/S3/WebDAV)
    // into ffprobe's stdin. Running the local core-count worth of them in
    // parallel saturates the backend link, so every stream reads slower than
    // the 30s probe timeout and the whole batch fails. Cap remote probes far
    // below the local limit — the bottleneck is link bandwidth, not CPU.
    public static int MaxConcurrentRemoteProbes { get; } = 2;

    private static readonly SemaphoreSlim Semaphore = new(MaxConcurrentProbes, MaxConcurrentProbes);

    private static readonly SemaphoreSlim RemoteSemaphore = new(
        MaxConcurrentRemoteProbes,
        MaxConcurrentRemoteProbes
    );

    /// <summary>
    /// Acquires a slot to run an ffprobe process. Blocks until a slot is available.
    /// </summary>
    public static Task WaitAsync(CancellationToken ct = default) => Semaphore.WaitAsync(ct);

    /// <summary>
    /// Acquires a slot asynchronously with a timeout. Returns true if acquired, false on timeout.
    /// </summary>
    public static Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct = default) =>
        Semaphore.WaitAsync(timeout, ct);

    /// <summary>
    /// Releases a slot after an ffprobe process has completed.
    /// </summary>
    public static void Release() => Semaphore.Release();

    /// <summary>
    /// Acquires the tighter remote-probe slot (in addition to the general slot)
    /// so concurrent network-streamed probes stay well below the backend's link
    /// capacity. Returns true if acquired, false on timeout.
    /// </summary>
    public static Task<bool> WaitRemoteAsync(TimeSpan timeout, CancellationToken ct = default) =>
        RemoteSemaphore.WaitAsync(timeout, ct);

    /// <summary>
    /// Releases the remote-probe slot after a network-streamed probe completes.
    /// </summary>
    public static void ReleaseRemote() => RemoteSemaphore.Release();
}
