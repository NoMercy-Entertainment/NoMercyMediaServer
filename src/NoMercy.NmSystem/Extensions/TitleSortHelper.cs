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

using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Extensions;

public static class TitleSortHelper
{
    private static string _parseTitleSort(string? value = null, DateTime? date = null)
    {
        if (string.IsNullOrWhiteSpace(value: value))
            return "";

        // Remove leading "The ", "An ", "A " (case-insensitive)
        value = Regex.Replace(input: value, pattern: @"^(The|An|A)\s+", replacement: "", options: RegexOptions.IgnoreCase);

        // Replace ": " and " and the " with the year if available
        if (date != null)
        {
            string year = date.Value.Year.ToString();
            value = Regex.Replace(input: value, pattern: @"[:]\s| and the ", replacement: $".{year}.", options: RegexOptions.IgnoreCase);
        }

        // Replace multiple dots with a space (keeps readability)
        value = Regex.Replace(input: value, pattern: @"\.+", replacement: " ");

        // Sanitize file name to remove unwanted characters
        value = value.CleanFileName();

        return value.ToLower().Trim();
    }

    public static string TitleSort(this object self, int? parseYear)
    {
        return _parseTitleSort(
            value: self.ToString(),
            date: parseYear != null ? new DateTime(year: parseYear.Value, month: 1, day: 1) : null
        );
    }

    public static string TitleSort<T>(this T? self, DateTime? date = null)
    {
        return _parseTitleSort(value: self?.ToString(), date: date);
    }
}
