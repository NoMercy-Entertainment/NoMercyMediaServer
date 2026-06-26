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

using System.Collections.Concurrent;
using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Extensions;

public static class ConcurrentBag
{
    public static ConcurrentBag<T> ToConcurrentBag<T>(this IEnumerable<T> self)
        where T : class
    {
        if (self is ConcurrentBag<T> concurrentBag)
            return concurrentBag;
        return new(self);
    }

    public static ConcurrentBag<MediaFile> FilterConcurrentBag(
        this ConcurrentBag<MediaFile> self,
        string[] filterFiles
    )
    {
        self = self.Where(f => filterFiles.Any(s => f.Name == s || f.Path.Contains(s)))
            .ToConcurrentBag();
        return self;
    }
}
