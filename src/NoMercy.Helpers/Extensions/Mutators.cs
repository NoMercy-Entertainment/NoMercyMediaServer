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

namespace NoMercy.Helpers.Extensions;

public static class Mutators
{
    // public static void Shuffle<T>(this IList<T> list)
    // {
    //     int n = list.Count;
    //     while (n > 1) {
    //         n--;
    //         int k = rand.Next(n + 1);
    //         (list[k], list[n]) = (list[n], list[k]);
    //     }
    // }
    public static IEnumerable<T> Randomize<T>(this IEnumerable<T> source)
    {
        Random rnd = new();
        return source.OrderBy(_ => rnd.Next());
    }
}
