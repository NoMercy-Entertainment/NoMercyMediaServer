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

public static class Culture
{
    private static readonly Dictionary<string, string> LegacyIsoMap = new()
    {
        { "bod", "tib" }, // Tibetan
        { "ces", "cze" }, // Czech
        { "cym", "wel" }, // Welsh
        { "deu", "ger" }, // German
        { "ell", "gre" }, // Greek
        { "eus", "baq" }, // Basque
        { "fas", "per" }, // Persian
        { "fra", "fre" }, // French
        { "hye", "arm" }, // Armenian
        { "isl", "ice" }, // Icelandic
        { "kat", "geo" }, // Georgian
        { "mkd", "mac" }, // Macedonian
        { "mri", "mao" }, // Maori
        { "msa", "may" }, // Malay
        { "mya", "bur" }, // Burmese
        { "nld", "dut" }, // Dutch
        { "ron", "rum" }, // Romanian
        { "slk", "slo" }, // Slovak
        { "sqi", "alb" }, // Albanian
        { "zho", "chi" }, // Chinese
    };

    /// <summary>
    /// Returns the English language tag for the given CultureInfo.
    /// Format: "ISO639-2 - EnglishName", e.g., "dut - Dutch (Netherlands)"
    /// </summary>
    public static string EnglishLanguageTag(this CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        string iso3 = culture.ThreeLetterISOLanguageName;

        // Only replace if the language is not English
        bool isEnglish = iso3.Equals("eng", StringComparison.OrdinalIgnoreCase);
        string tag =
            !isEnglish && LegacyIsoMap.TryGetValue(iso3, out string? legacyCode)
                ? legacyCode
                : iso3;

        return tag;
    }

    private static readonly Dictionary<string, string> EnglishNameByCode = BuildEnglishNameMap();

    private static Dictionary<string, string> BuildEnglishNameMap()
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        foreach (CultureInfo c in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            string iso2 = c.TwoLetterISOLanguageName;
            string iso3 = c.ThreeLetterISOLanguageName;
            string name = StripRegion(c.EnglishName);

            if (iso2.Length == 2)
                map.TryAdd(iso2, name);
            if (iso3.Length == 3)
                map.TryAdd(iso3, name);
        }

        // Map the legacy/bibliographic codes to the same English name as the
        // ISO 639-3 form (deu→ger, fra→fre, nld→dut etc).
        foreach (KeyValuePair<string, string> entry in LegacyIsoMap)
        {
            if (map.TryGetValue(entry.Key, out string? name))
                map.TryAdd(entry.Value, name);
        }

        // Fixed display labels for codes the runtime doesn't carry.
        map.TryAdd("und", "Unknown");
        map.TryAdd("mul", "Multiple Languages");
        map.TryAdd("zxx", "No Language");

        return map;
    }

    /// <summary>
    /// Returns the English display name for an ISO 639 language code (2 or 3
    /// letter). Falls back to the original code uppercased when no match
    /// exists — never throws, never returns null.
    /// </summary>
    public static string EnglishLanguageName(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "Unknown";

        return EnglishNameByCode.TryGetValue(code, out string? name)
            ? name
            : code.ToUpperInvariant();
    }

    private static string StripRegion(string englishName)
    {
        // CultureInfo.EnglishName is "Dutch (Netherlands)" / "English (United States)"
        // for neutral cultures the parenthesis is absent, but defensive trim
        // covers both shapes.
        int paren = englishName.IndexOf(' ', StringComparison.Ordinal);
        if (paren < 0)
            return englishName;

        if (englishName[paren..].StartsWith(" (", StringComparison.Ordinal))
            return englishName[..paren];

        return englishName;
    }
}
