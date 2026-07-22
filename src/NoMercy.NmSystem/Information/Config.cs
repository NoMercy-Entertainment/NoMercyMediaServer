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

namespace NoMercy.NmSystem.Information;

public static class Config
{
    public static string ManagementPipeName
    {
        get => field ?? "NoMercyManagement";
        set;
    }

    public static string ManagementSocketPath =>
        Path.Combine(path1: AppFiles.AppPath, path2: "nomercy-management.sock");

    public static bool IsDev { get; set; }
    public static bool IsTest { get; set; }
}
