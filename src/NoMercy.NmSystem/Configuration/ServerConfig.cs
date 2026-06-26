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

namespace NoMercy.NmSystem.Configuration;

public class ServerConfig
{
    public int InternalServerPort { get; set; } = 7626;
    public int ExternalServerPort { get; set; } = 7626;
    public bool Swagger { get; set; } = true;
    public bool IsDev { get; set; }
    public bool IsTest { get; set; }
    public string ManagementPipeName { get; set; } = "NoMercyManagement";
}
