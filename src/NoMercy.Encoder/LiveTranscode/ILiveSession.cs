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

    /// <summary>
    /// Zero-based index among the source's audio streams currently being mapped
    /// (<c>0:a:N</c>). Read at every runner (re)spawn so a seek or quality change
    /// keeps the track resolved from the library's language preference.
    /// </summary>
    int CurrentAudioStreamIndex { get; }

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

    /// <summary>
    /// Updates the playhead used to compute <see cref="BufferAhead"/>.
    /// <paramref name="authoritative"/> true (a client heartbeat, a seek, or the
    /// encode start position) always applies; false (the segment-request-derived
    /// prefetch frontier) applies only while no authoritative report is still
    /// within its authority window, so a live client's true position is never
    /// overwritten by how far ahead the player has prefetched.
    /// </summary>
    void ReportPlaybackPosition(TimeSpan position, bool authoritative);

    /// <summary>
    /// UTC time the current FFmpeg runner was (re)started — session start, seek,
    /// quality change, or resume. <see cref="DateTime.MinValue"/> until the first
    /// runner is spawned. The buffer-adaptive sweep uses it as a warm-up grace so
    /// it does not act on the legitimately-empty buffer of a runner that has not
    /// yet written its first segment.
    /// </summary>
    DateTime LastTranscodeStart { get; }

    /// <summary>
    /// Stamps <see cref="LastTranscodeStart"/> with the current UTC time. Called
    /// at every point a new runner is dispatched.
    /// </summary>
    void MarkTranscodeStart();

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
