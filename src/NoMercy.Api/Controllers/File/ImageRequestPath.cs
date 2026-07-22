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

namespace NoMercy.Api.Controllers.File;

/// <summary>
/// The image endpoint is anonymous and joins its <c>type</c>/<c>path</c> route
/// segments straight into a filesystem path. Each segment is meant to be a single
/// name, so this reduces any value to its final path component — neutralising
/// directory traversal (notably Windows <c>..\</c> via <c>%5C</c>, which a plain
/// <c>Replace("/","")</c> misses) on every platform before the path is served.
/// </summary>
public static class ImageRequestPath
{
    public static string SanitizeSegment(string? segment)
    {
        if (string.IsNullOrEmpty(value: segment))
            return string.Empty;

        string normalised = segment.Replace(oldChar: '\\', newChar: '/');
        int lastSlash = normalised.LastIndexOf(value: '/');
        string name = lastSlash >= 0 ? normalised[(lastSlash + 1)..] : normalised;

        // A bare "." / ".." (no separator) would still escape via Path.Join.
        return name is "." or ".." ? string.Empty : name;
    }
}
