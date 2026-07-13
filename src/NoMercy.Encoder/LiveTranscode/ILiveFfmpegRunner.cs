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

using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.LiveTranscode;

public record LiveRunInput(
    string InputPath,
    string OutputDirectory,
    TimeSpan StartPosition,
    LiveQuality Quality,
    int SegmentDurationSeconds,
    ClientCapabilities? Client = null,
    MediaInfo? SourceInfo = null,
    Dictionary<string, string>? CustomArguments = null,
    // ffmpeg input options emitted before "-i" (e.g. "-headers" for an
    // authenticated HTTP self-ingest URL). Null for a plain filesystem input.
    string[]? ExtraInputArgs = null,
    // Zero-based index AMONG AUDIO STREAMS to map (0:a:N). Chosen by
    // LiveAudioSelector from the viewer's language preference; 0 is the file's
    // first audio track. Only consulted when audio is muxed (VideoOnly is false).
    int AudioStreamIndex = 0,
    // Emit video only ("-an"). The master playlist references the file's own
    // pre-encoded audio renditions, so muxing audio here would waste CPU and
    // break instant track switching.
    bool VideoOnly = false
);

/// <summary>
/// Spawns an FFmpeg HLS live transcode process for one session and feeds each
/// completed segment into the session's channel. Runs until the process exits
/// or the session is cancelled.
/// </summary>
public interface ILiveFfmpegRunner
{
    Task RunAsync(LiveRunInput input, LiveSession session, CancellationToken ct);
}
