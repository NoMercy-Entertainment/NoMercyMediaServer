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

public record LadderRung(
    int Width,
    int Height,
    VideoCodecType Codec,
    int BitrateKbps,
    int MaxBitrateKbps,
    int BufferSizeKbps,
    double Framerate,
    string? Preset = null,
    CodecProfile CodecProfile = CodecProfile.Auto,
    int BitDepth = 8,
    string? PixelFormat = null
);
