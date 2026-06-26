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

namespace NoMercy.NmSystem.Status;

public interface IUpdateStatus
{
    bool UpdateAvailable { get; set; }
    bool RestartNeeded { get; set; }
    string? LatestVersion { get; set; }
}

public class UpdateStatus : IUpdateStatus
{
    public bool UpdateAvailable { get; set; }
    public bool RestartNeeded { get; set; }
    public string? LatestVersion { get; set; }
}
