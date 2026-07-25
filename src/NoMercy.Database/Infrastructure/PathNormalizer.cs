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

namespace NoMercy.Database.Infrastructure;

internal static class PathNormalizer
{
    public static string Normalize(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\\', '/');

    public static string? NormalizeNullable(string? value) =>
        string.IsNullOrEmpty(value) ? value : value.Replace('\\', '/');
}
