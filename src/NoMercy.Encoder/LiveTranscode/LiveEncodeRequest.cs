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

public record LiveEncodeRequest(
    string InputPath,
    MediaInfo CachedInfo,
    ClientCapabilities Client,
    TimeSpan StartPosition,
    string? PreferredQuality,
    Dictionary<string, string>? CustomArguments = null,
    // ffmpeg input options emitted before "-i" — carries the auth header when
    // InputPath is an authenticated HTTP self-ingest URL. Null for a filesystem input.
    string[]? ExtraInputArgs = null,
    // Zero-based index among the source's audio streams to map by default,
    // resolved from the library's language preference by LiveAudioSelector.
    // Only consulted when audio is muxed (VideoOnly is false).
    int AudioStreamIndex = 0,
    // Drop audio entirely and transcode video only. Set when the source already
    // ships browser-ready HLS audio renditions the master playlist references, so
    // re-encoding audio would be wasted work and would defeat instant switching.
    bool VideoOnly = false
);
