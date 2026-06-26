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

using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.OpticalMedia.Sources;

/// <summary>
/// Per-disc-type reader. Implementations live in <c>Bluray/</c>, <c>Dvd/</c>,
/// <c>AudioCd/</c>, <c>DataCd/</c>. Selected at runtime by
/// <see cref="DiscSourceFactory"/> based on <see cref="DiscDrive.DiscType"/>.
/// </summary>
public interface IDiscSource
{
    OpticalDiscType Type { get; }

    /// <summary>
    /// Cheap full-disc probe: enumerates titles and their durations without
    /// reading per-title stream data. Fast — used for the disc-inserted UX.
    /// Per-title <see cref="DiscTitle.VideoStreams"/> /
    /// <see cref="DiscTitle.AudioStreams"/> /
    /// <see cref="DiscTitle.Subtitles"/> /
    /// <see cref="DiscTitle.Chapters"/> may be empty arrays — call
    /// <see cref="ProbeTitleAsync"/> when those are needed.
    /// </summary>
    Task<DiscInfo> ProbeAsync(DiscDrive drive, CancellationToken ct);

    /// <summary>
    /// Detailed per-title probe: returns the full <see cref="DiscTitle"/>
    /// with streams, languages, chapters. Slow on Blu-ray (one ffprobe call
    /// per title). Call lazily — when the user opens browse/rip details.
    /// </summary>
    Task<DiscTitle> ProbeTitleAsync(DiscDrive drive, int titleIndex, CancellationToken ct);
}
