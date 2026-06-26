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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.BuildingBlocks;

/// <summary>
/// Generates an adaptive-bitrate quality ladder from source media.
/// </summary>
public interface IAbrLadderGenerator
{
    /// <summary>
    /// Legacy path: generates <see cref="VideoOutput"/> rungs using hardcoded
    /// tier bitrates scaled by source complexity.
    /// </summary>
    VideoOutput[] Generate(MediaInfo media, VideoOutput reference);

    /// <summary>
    /// New path: generates <see cref="LadderRung"/> rungs by reading every
    /// parameter from <paramref name="autoConfig"/> — no hardcoded constants.
    /// When <paramref name="reference"/> is supplied, each rung inherits its
    /// BitDepth, PixelFormat, Preset, and CodecProfile so HDR / 10-bit profiles
    /// survive the ladder expansion (an SDR source still resolves to 8-bit
    /// via the reference profile's own defaults).
    /// </summary>
    LadderRung[] GenerateLadder(
        MediaInfo media,
        VideoCodecType profileCodec,
        AutoLadderConfig autoConfig,
        VideoOutput? reference = null
    );
}
