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

namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveEncoder
{
    Task<ILiveSession> StartAsync(LiveEncodeRequest request, CancellationToken ct);

    /// <summary>
    /// Starts an audio-only transcode for a single source language (a child of a
    /// video session), used for a raw source with no pre-encoded renditions. It
    /// does not count against the concurrent-session cap and produces an AAC HLS
    /// track the master playlist references. The audio stream to map is taken from
    /// <see cref="LiveEncodeRequest.AudioStreamIndex"/>.
    /// </summary>
    Task<ILiveSession> StartAudioRenditionAsync(LiveEncodeRequest request, CancellationToken ct);
}
