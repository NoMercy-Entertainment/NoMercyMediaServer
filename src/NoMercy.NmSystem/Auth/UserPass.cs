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

namespace NoMercy.NmSystem.Auth;

public class UserPass
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string? ApiKey { get; set; }

    public UserPass(string username, string password, string apiKey)
    {
        Username = username;
        Password = password;
        ApiKey = apiKey;
    }
}
