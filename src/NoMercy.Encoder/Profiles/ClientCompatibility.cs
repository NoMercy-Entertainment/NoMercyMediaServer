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

[Flags]
public enum ClientCompatibility
{
    None = 0,
    BrowserMse = 1 << 0,
    NativeAndroid = 1 << 1,
    NativeIos = 1 << 2,
    Cast = 1 << 3,
    LegacyDevices = 1 << 4,
    Universal = BrowserMse | NativeAndroid | NativeIos | Cast,
}
