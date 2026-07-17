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

namespace NoMercy.Encoder.Commands;

/// <summary>
/// One <c>-metadata:s:*</c> tag on an output stream. A list is used rather than a
/// dictionary because a single stream carries several tags (language, title, …)
/// under the same <c>-metadata:s:a:0</c> flag, which a dictionary cannot hold.
/// An empty <see cref="Value"/> clears whatever the source set — ffmpeg treats
/// <c>-metadata:s:v:0 language=</c> as "remove this tag".
/// </summary>
public readonly record struct OutputStreamTag(string StreamSpecifier, string Key, string Value);

public record OutputOptions(
    string FilePath,
    string? VideoCodec = null,
    string? AudioCodec = null,
    string? SubtitleCodec = null,
    int? VideoBitrateKbps = null,
    int? AudioBitrateKbps = null,
    int? Crf = null,
    string? Preset = null,
    string? Profile = null,
    string? Level = null,
    string? PixelFormat = null,
    int? KeyframeInterval = null,
    string? AudioChannels = null,
    int? AudioSampleRate = null,
    string[]? MapStreams = null,
    Dictionary<string, string>? ExtraFlags = null,
    // Drop the container-level tags the source carried (encoder name, ripper
    // group, comments) rather than letting ffmpeg copy them into the output.
    bool StripSourceMetadata = false,
    // Per-stream tags this output stamps. Overrides whatever the source stream
    // carried, which ffmpeg copies regardless of StripSourceMetadata.
    IReadOnlyList<OutputStreamTag>? StreamMetadata = null
);
