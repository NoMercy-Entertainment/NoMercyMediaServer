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

namespace NoMercy.Encoder.ContentAnalysis;

/// <summary>
/// The settings a probe encodes its sample with — the quality-determining
/// subset of a <c>VideoOutput</c>, kept separate so the probe does not depend on
/// the profile model and can be exercised on its own.
/// </summary>
public record EncodeYieldTarget(
    VideoCodecType Codec,
    int Crf,
    string? Preset,
    string? Tune,
    string? PixelFormat
);

/// <summary>
/// Answers "if we re-encoded this source with these settings, what bitrate would
/// come out?" by encoding a short sample and measuring it.
///
/// A CRF target states a quality, not a size, and the size it lands on is a
/// property of the content: measured on one 1080p animation source, x265 CRF 20
/// produced 2.05 Mbps where the rule of thumb (halve per +6 CRF) predicted
/// nowhere near it. Nothing short of encoding some of the actual frames answers
/// the question honestly, which is why this exists rather than a lookup table.
/// </summary>
public interface IEncodeYieldProbe
{
    /// <summary>
    /// Estimated output bitrate in kbps, or <c>null</c> when it could not be
    /// measured. Callers must treat null as "unknown" and fall back to their
    /// safe default — never as "zero".
    /// </summary>
    Task<long?> EstimateBitrateKbpsAsync(
        string inputPath,
        EncodeYieldTarget target,
        TimeSpan sourceDuration,
        CancellationToken ct
    );
}
