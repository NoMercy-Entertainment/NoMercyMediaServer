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

namespace NoMercy.NmSystem.Extensions;

public static class ConditionalSetExtensions
{
    public static T? GetIf<T>(this T? source, bool condition)
        where T : class
    {
        return condition && source != null ? source : null;
    }

    public static T? GetIfNotNull<T>(this T? source)
        where T : class
    {
        return source;
    }
}
