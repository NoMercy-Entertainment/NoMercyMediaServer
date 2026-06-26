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
    Dictionary<string, string>? ExtraFlags = null
);
