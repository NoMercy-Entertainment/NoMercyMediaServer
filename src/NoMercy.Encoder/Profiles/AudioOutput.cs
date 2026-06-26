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

using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public record AudioOutput(
    StreamPolicy Policy,
    AudioCodecType Codec,
    int BitrateKbps,
    int Channels,
    int SampleRateHz,
    string[] AllowedLanguages,
    string? DefaultLanguage,
    LoudnessConfig? Loudness,
    DownmixConfig? Downmix,
    string SegmentNameTemplate,
    string PlaylistNameTemplate,
    Dictionary<string, string>? CustomArguments = null
);
