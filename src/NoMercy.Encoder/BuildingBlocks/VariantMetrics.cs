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

namespace NoMercy.Encoder.BuildingBlocks;

/// <summary>
/// Measured bitrate metrics (peak + average, bits/sec) for a single HLS
/// variant playlist. Promoted out of HlsVariantAnalyzer so the
/// IHlsVariantAnalyzer / IPlaylistGenerator interfaces no longer depend on a
/// concrete implementation's nested type.
/// </summary>
public record VariantMetrics(int PeakBandwidth, int AverageBandwidth);
