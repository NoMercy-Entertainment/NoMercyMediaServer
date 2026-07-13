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

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Authoritative "does this hardware encoder actually work on this machine"
/// probe. <see cref="IFfmpegCapabilities.AvailableEncoders"/> only reflects
/// which encoders the running ffmpeg binary was compiled with — the NoMercy
/// fork advertises every hardware encoder family it knows how to build
/// regardless of installed silicon or driver. A name appearing there is not
/// evidence it will actually initialize.
///
/// <see cref="ProbeAsync"/> runs a minimal real encode (one frame, to null)
/// per candidate through the real ffmpeg binary and returns only the names
/// that actually initialized. Software encoders are never passed in — they
/// are unconditionally usable and probing them wastes a process spawn.
/// </summary>
public interface IHardwareEncoderProbe
{
    /// <summary>
    /// Probes every candidate hardware encoder name and returns the subset
    /// that successfully initializes. A probe failure (nonzero exit, hang,
    /// or an unrecognised encoder family) excludes that name from the
    /// result — it never throws for a single candidate's failure.
    /// </summary>
    Task<IReadOnlySet<string>> ProbeAsync(
        IEnumerable<string> candidateHardwareEncoders,
        CancellationToken ct = default
    );
}
