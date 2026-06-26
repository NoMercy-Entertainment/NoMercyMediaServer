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

namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveSession : IAsyncDisposable
{
    string SessionId { get; }
    LiveSessionState State { get; }
    LiveQuality CurrentQuality { get; }
    double CurrentSpeed { get; }
    TimeSpan TranscodedPosition { get; }
    TimeSpan BufferAhead { get; }
    IAsyncEnumerable<Segment> Segments { get; }

    /// <summary>
    /// Tears down the current FFmpeg runner and spawns a new one starting at
    /// <paramref name="position"/>. The caller receives control back once the
    /// new runner is dispatched; segment flow resumes asynchronously.
    /// </summary>
    Task SeekAsync(TimeSpan position, CancellationToken ct);

    /// <summary>
    /// Tears down the current FFmpeg runner and spawns a new one using
    /// <paramref name="newQuality"/>. Resumes from the current playback
    /// position so the viewer does not jump backward.
    /// </summary>
    Task ChangeQualityAsync(string qualityId, LiveQuality newQuality, CancellationToken ct);
    void Suspend();
    void Resume();
    void ReportPlaybackPosition(TimeSpan position);

    /// <summary>
    /// Attaches the factory that <see cref="SeekAsync"/> uses to spawn a
    /// replacement runner. Called once by <see cref="LiveEncoder"/> immediately
    /// after the session is created.
    /// </summary>
    void AttachRunnerFactory(Func<TimeSpan, CancellationToken, Task> factory);

    /// <summary>
    /// Registers a callback invoked at the start of every seek and quality
    /// change so the runtime buffer can be purged before the new runner fires.
    /// Called once by <see cref="LiveStreamingService"/> after registration.
    /// </summary>
    void AttachBufferResetCallback(Action callback);
}
