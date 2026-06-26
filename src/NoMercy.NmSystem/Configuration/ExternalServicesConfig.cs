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

public class ExternalServicesConfig
{
    public string AuthBaseUrl { get; set; } = "https://auth.nomercy.tv/realms/NoMercyTV/";
    public string AppBaseUrl { get; set; } = "https://app.nomercy.tv/";
    public string ApiBaseUrl { get; set; } = "https://api.nomercy.tv/";
    public string TokenClientId { get; set; } = "nomercy-server";
}
