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
using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveQualitySelector
{
    LiveQuality[] GetAvailableQualities(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IResourceBudget budget
    );

    LiveQuality SelectOptimal(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IResourceBudget budget
    );

    /// <summary>
    /// Fits a quality tier directly to a client's observed downlink: the
    /// highest tier in <paramref name="available"/> whose bitrate does not
    /// exceed <c>observedBandwidthKbps * usableFraction</c>. Never returns
    /// empty — falls back to the lowest tier when nothing fits, and to
    /// <paramref name="current"/> when <paramref name="available"/> itself is
    /// empty, so a caller is never left without a quality to select.
    /// </summary>
    LiveQuality SelectForBandwidth(
        LiveQuality[] available,
        int observedBandwidthKbps,
        double usableFraction,
        LiveQuality current
    );
}
