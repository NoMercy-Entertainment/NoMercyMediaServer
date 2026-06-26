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

using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Receives per-task progress events while a task is executing. Remote
/// workers wire this to an HTTP pusher so the coordinator sees live
/// progress; standalone installs use a no-op sink.
///
/// Implementations must tolerate being called from the ffmpeg progress
/// thread (usually every 500ms) without throwing — a failing sink must
/// not bring down the encode. Buffering / batching is the impl's
/// responsibility.
/// </summary>
public interface ITaskProgressSink
{
    /// <summary>
    /// Called each time ffmpeg emits a progress snapshot for the running
    /// task. Thread-safe contract: sinks must not assume single-threaded
    /// access.
    /// </summary>
    void Report(string taskId, EncodingProgress progress);
}

/// <summary>
/// Default sink for standalone / coordinator installs — drops progress
/// events on the floor. Wiring a different sink in DI enables push to
/// remote coordinators.
/// </summary>
public sealed class NullTaskProgressSink : ITaskProgressSink
{
    public void Report(string taskId, EncodingProgress progress) { }
}
