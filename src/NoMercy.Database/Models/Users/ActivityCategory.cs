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

namespace NoMercy.Database.Models.Users;

public enum ActivityCategory
{
    Auth = 1,
    Connection = 2,
    Playback = 3,
    Configuration = 4,
    Failure = 5,

    /// <summary>Work the encoder did: a job starting, finishing, or giving up.</summary>
    Encoder = 6,

    /// <summary>Content arriving or leaving: scans, and files being imported.</summary>
    Library = 7,
}
