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

namespace NoMercy.Encoder.LiveTranscode;

public record ClientCapabilities(
    VideoCodecType[] SupportedVideoCodecs,
    AudioCodecType[] SupportedAudioCodecs,
    string[] SupportedContainers,
    int MaxWidth,
    int MaxHeight,
    bool SupportsHdr,
    bool Supports10Bit,
    int MaxBitrateKbps,
    int MaxAudioChannels = 2
);
