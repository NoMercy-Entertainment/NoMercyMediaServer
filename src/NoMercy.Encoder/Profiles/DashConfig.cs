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

namespace NoMercy.Encoder.Profiles;

public record DashConfig(
    int MinBufferTimeSeconds = 4,
    bool SegmentTemplate = true, // SegmentTemplate vs SegmentList
    bool UseTimeline = true, // SegmentTimeline inside SegmentTemplate
    int? MaxSegmentDurationSeconds = null, // override vs profile.SegmentDurationSeconds
    string? Profile = "urn:mpeg:dash:profile:isoff-live:2011"
);
