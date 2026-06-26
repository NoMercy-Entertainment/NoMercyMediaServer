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

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// One ABR variant in the encoding plan — combines the video target,
/// its paired audio tracks, and the HLS segmentation parameters. The
/// dashboard renders each <see cref="VariantPlan"/> as a row in the
/// variant table (e.g. "1080p · H.264 · CRF 23 · en/aac@192").
/// </summary>
public sealed record VariantPlan(
    string VariantId,
    VideoTarget Video,
    IReadOnlyList<AudioTarget> Audio,
    int SegmentDurationSeconds,
    int KeyframeIntervalSeconds
);
