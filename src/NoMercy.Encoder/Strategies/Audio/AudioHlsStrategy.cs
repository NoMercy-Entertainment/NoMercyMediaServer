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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

namespace NoMercy.Encoder.Strategies.Audio;

/// <summary>
/// Audio-only HLS strategy. Encodes audio input to AAC segments and an
/// <c>audio.m3u8</c> VOD playlist. No video, no subtitles, no master
/// playlist — the music player (hls.js) loads the variant playlist directly.
/// </summary>
public class AudioHlsStrategy(IEncoder encoder, ILogger<AudioHlsStrategy> logger, IStorage storage)
    : SinglePassStrategyBase(encoder, logger, storage)
{
    public override OutputFormat Format => OutputFormat.AudioHls;
}
