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

using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NoMercy.Storage;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.NmSystem.Extensions;

public static class FuzzyMatcher
{
    public static double MatchPercentage(string strA, string strB)
    {
        if (string.IsNullOrEmpty(strA) || string.IsNullOrEmpty(strB))
            return 0;

        int distance = LevenshteinDistance(strA.ToLower(), strB.ToLower());
        int maxLength = Math.Max(strA.Length, strB.Length);

        return (1.0 - (double)distance / maxLength) * 100;
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        // Single-row algorithm: O(n) space instead of O(n*m)
        int[] prev = new int[s2.Length + 1];
        int[] curr = new int[s2.Length + 1];

        for (int j = 0; j <= s2.Length; j++)
            prev[j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(
                        prev[j] + 1, // Deletion
                        curr[j - 1] + 1
                    ), // Insertion
                    prev[j - 1] + cost
                ); // Substitution
            }

            (prev, curr) = (curr, prev);
        }

        return prev[s2.Length];
    }

    public static List<T> SortByMatchPercentage<T>(
        IEnumerable<T> array,
        Func<T, string> keySelector,
        string match
    )
        where T : class
    {
        return array.OrderBy(item => MatchPercentage(match, keySelector(item))).ToList();
    }

    public static List<T> ToSortByMatchPercentage<T>(
        this IEnumerable<T> array,
        Func<T, string> keySelector,
        string match
    )
        where T : class
    {
        return array.OrderBy(item => MatchPercentage(match, keySelector(item))).ToList();
    }
}
