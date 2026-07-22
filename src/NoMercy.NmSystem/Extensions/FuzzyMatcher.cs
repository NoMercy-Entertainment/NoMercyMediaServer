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

public static class FuzzyMatcher
{
    public static double MatchPercentage(string strA, string strB)
    {
        if (string.IsNullOrEmpty(value: strA) || string.IsNullOrEmpty(value: strB))
            return 0;

        int distance = LevenshteinDistance(s1: strA.ToLower(), s2: strB.ToLower());
        int maxLength = Math.Max(val1: strA.Length, val2: strB.Length);

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
                int cost = s1[index: i - 1] == s2[index: j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    val1: Math.Min(
                        val1: prev[j] + 1, // Deletion
                        val2: curr[j - 1] + 1
                    ), // Insertion
                    val2: prev[j - 1] + cost
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
        return array.OrderBy(keySelector: item => MatchPercentage(strA: match, strB: keySelector(arg: item))).ToList();
    }

    public static List<T> ToSortByMatchPercentage<T>(
        this IEnumerable<T> array,
        Func<T, string> keySelector,
        string match
    )
        where T : class
    {
        return array.OrderBy(keySelector: item => MatchPercentage(strA: match, strB: keySelector(arg: item))).ToList();
    }
}
