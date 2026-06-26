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
}
