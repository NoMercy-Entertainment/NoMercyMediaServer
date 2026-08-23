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

using NoMercy.Encoder.LiveTranscode;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Live;

/// <summary>
/// Bridges an optical-disc title into the existing live HLS encoder.
/// Each instance owns one ffmpeg-fed live session — call
/// <see cref="StartAsync"/> with a drive + title index, get back an
/// <see cref="ILiveSession"/> the web player can pull HLS from.
/// </summary>
public interface ILiveDiscSession
{
    /// <param name="audioTracks">
    /// Disc audio streams to expose in the live master playlist, using the same
    /// <see cref="AudioTrackSelection"/> shape the rip endpoint already takes
    /// (<c>StreamIndex</c> = the disc's ffmpeg audio stream index, <c>Include</c>
    /// = whether to expose it). Zero or one included track keeps the existing
    /// single-track muxed behaviour (backwards compatible for callers that pass
    /// an empty array). Two or more spawns one video-only session plus one
    /// audio-only rendition per included track, the same pattern
    /// LiveTranscodeService already uses for raw multi-audio file sources —
    /// the first included track is the default.
    /// </param>
    Task<ILiveSession> StartAsync(
        DiscDrive drive,
        int titleIndex,
        TimeSpan startPosition,
        string? preferredQuality,
        AudioTrackSelection[] audioTracks,
        CancellationToken ct
    );
}
