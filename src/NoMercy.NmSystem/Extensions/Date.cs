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

using System.Globalization;

namespace NoMercy.NmSystem.Extensions;

public static class Date
{
    private static readonly string[] ValidFormats =
    [
        "yyyy",
        "MM-yyyy",
        "dd-MM-yyyy",
        "dd-MM-yyyy HH:mm:ss",
        "yyyy-MM",
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH",
        // American notations of time
        "MM/dd/yyyy",
        "MM/dd/yyyy HH:mm:ss",
        "MM/dd/yyyy hh:mm:ss tt", // 12-hour clock with AM/PM
        "MM/dd/yyyy hh:mm tt", // 12-hour clock with AM/PM, no seconds
        "M/d/yyyy",
        "M/d/yyyy HH:mm:ss",
        "M/d/yyyy hh:mm:ss tt",
        "M/d/yyyy hh:mm tt",
        "MM-dd-yyyy",
        "MM-dd-yyyy HH:mm:ss",
        "MM-dd-yyyy hh:mm:ss tt", // 12-hour clock with AM/PM
        "MM-dd-yyyy hh:mm tt", // 12-hour clock with AM/PM, no seconds
        "M-d-yyyy",
        "M-d-yyyy HH:mm:ss",
        "M-d-yyyy hh:mm:ss tt",
        "M-d-yyyy hh:mm tt",
    ];

    private static int _parseYear(DateTime? dateString = null)
    {
        return dateString?.Year ?? 0;
    }

    public static string ToHms(this int seconds)
    {
        return TimeSpan.FromSeconds(seconds: seconds).ToString();
    }

    public static bool TryParseToDateTime(this string value, out DateTime dateTime)
    {
        return DateTime.TryParseExact(
                s: value,
                formats: ValidFormats,
                provider: CultureInfo.InvariantCulture,
                style: DateTimeStyles.None,
                result: out dateTime
            )
            || (
                DateTime.TryParse(s: value, provider: DateTimeFormatInfo.InvariantInfo, result: out dateTime)
                && dateTime != default
            );
    }

    public static DateTime SubDays(this DateTime self, int days)
    {
        if (days < 0)
            throw new ArgumentOutOfRangeException(paramName: nameof(days), message: "Days must be positive.");
        return self.Subtract(value: new TimeSpan(days: days, hours: 0, minutes: 0, seconds: 0));
    }

    public static int ParseYear(this DateTime? self)
    {
        return string.IsNullOrEmpty(value: self.ToString()) ? 0 : _parseYear(dateString: self);
    }

    public static int ParseYear(this DateTime self)
    {
        return string.IsNullOrEmpty(value: self.ToString(provider: CultureInfo.InvariantCulture))
            ? 0
            : _parseYear(dateString: self);
    }

    public static string ToHis(this double time)
    {
        return TimeSpan.FromSeconds(value: time).ToString(format: @"hh\:mm\:ss\.fff");
    }

    public static string ToHis(this long time)
    {
        return TimeSpan.FromSeconds(seconds: time).ToString(format: @"hh\:mm\:ss\.fff");
    }

    public static string ToHumanTime(this int time)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds: time);
        if (t.TotalHours >= 1)
            return t.ToString(format: @"hh\:mm\:ss");
        return t.ToString(format: @"mm\:ss");
    }

    public static string ToHumanTime(this double time)
    {
        TimeSpan t = TimeSpan.FromSeconds(value: time);
        if (t.TotalHours >= 1)
            return t.ToString(format: @"hh\:mm\:ss");
        return t.ToString(format: @"mm\:ss");
    }

    public static string ToHumanTime(this long time)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds: time);
        if (t.TotalHours >= 1)
            return t.ToString(format: @"hh\:mm\:ss");
        return t.ToString(format: @"mm\:ss");
    }
}
