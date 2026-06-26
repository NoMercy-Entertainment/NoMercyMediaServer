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

public record VideoOutput(
    StreamPolicy Policy,
    VideoCodecType Codec,
    int Width,
    int? Height,
    RateControlMode RateControl,
    int Crf,
    int BitrateKbps,
    int? MaxBitrateKbps,
    int? BufferSizeKbps,
    string? Preset,
    CodecProfile CodecProfile,
    string? Level,
    string? Tune,
    int BitDepth,
    string? PixelFormat,
    int KeyframeIntervalSeconds,
    bool ConvertHdrToSdr,
    string SegmentNameTemplate,
    string PlaylistNameTemplate,
    Dictionary<string, string>? CustomArguments = null
);
