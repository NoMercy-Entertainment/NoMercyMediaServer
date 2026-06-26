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

using NoMercy.Encoder.LiveTranscode;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.OpticalMedia.Live;

/// <summary>
/// Bridges an optical-disc title into the existing live HLS encoder.
/// Each instance owns one ffmpeg-fed live session — call
/// <see cref="StartAsync"/> with a drive + title index, get back an
/// <see cref="ILiveSession"/> the web player can pull HLS from.
/// </summary>
public interface ILiveDiscSession
{
    Task<ILiveSession> StartAsync(
        DiscDrive drive,
        int titleIndex,
        TimeSpan startPosition,
        string? preferredQuality,
        CancellationToken ct
    );
}
