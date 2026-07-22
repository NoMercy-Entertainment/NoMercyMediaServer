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
        ArgumentNullException.ThrowIfNull(argument: culture);

        string iso3 = culture.ThreeLetterISOLanguageName;

        // Only replace if the language is not English
        bool isEnglish = iso3.Equals(value: "eng", comparisonType: StringComparison.OrdinalIgnoreCase);
        string tag =
            !isEnglish && LegacyIsoMap.TryGetValue(key: iso3, value: out string? legacyCode)
                ? legacyCode
                : iso3;

        return tag;
    }

    private static readonly Dictionary<string, string> Iso3ByIso2 = BuildIso3Map();

    private static Dictionary<string, string> BuildIso3Map()
    {
        Dictionary<string, string> map = new(comparer: StringComparer.OrdinalIgnoreCase);

        foreach (CultureInfo c in CultureInfo.GetCultures(types: CultureTypes.NeutralCultures))
        {
            string iso2 = c.TwoLetterISOLanguageName;
            string iso3 = c.ThreeLetterISOLanguageName;

            if (iso2.Length == 2 && iso3.Length == 3)
                map.TryAdd(key: iso2, value: iso3);
        }

        return map;
    }

    /// <summary>
    /// Returns the ISO 639-2/B (bibliographic) code for a 2- or 3-letter language code — "nl",
    /// "nl-NL" and "nld" all become "dut". Idempotent, so a code that is already bibliographic
    /// passes through unchanged, as does any code with no known mapping.
    /// </summary>
    public static string BibliographicLanguageCode(string code)
    {
        if (string.IsNullOrWhiteSpace(value: code))
            return code;

        string bare = code.Trim().Split(separator: ['-', '_'])[0].ToLowerInvariant();

        string iso3 =
            bare.Length == 2 && Iso3ByIso2.TryGetValue(key: bare, value: out string? mapped) ? mapped : bare;

        return LegacyIsoMap.TryGetValue(key: iso3, value: out string? bibliographic) ? bibliographic : iso3;
    }

    private static readonly Dictionary<string, string> EnglishNameByCode = BuildEnglishNameMap();

    private static Dictionary<string, string> BuildEnglishNameMap()
    {
        Dictionary<string, string> map = new(comparer: StringComparer.OrdinalIgnoreCase);

        foreach (CultureInfo c in CultureInfo.GetCultures(types: CultureTypes.NeutralCultures))
        {
            string iso2 = c.TwoLetterISOLanguageName;
            string iso3 = c.ThreeLetterISOLanguageName;
            string name = StripRegion(englishName: c.EnglishName);

            if (iso2.Length == 2)
                map.TryAdd(key: iso2, value: name);
            if (iso3.Length == 3)
                map.TryAdd(key: iso3, value: name);
        }

        // Map the legacy/bibliographic codes to the same English name as the
        // ISO 639-3 form (deu→ger, fra→fre, nld→dut etc).
        foreach (KeyValuePair<string, string> entry in LegacyIsoMap)
        {
            if (map.TryGetValue(key: entry.Key, value: out string? name))
                map.TryAdd(key: entry.Value, value: name);
        }

        // Fixed display labels for codes the runtime doesn't carry.
        map.TryAdd(key: "und", value: "Unknown");
        map.TryAdd(key: "mul", value: "Multiple Languages");
        map.TryAdd(key: "zxx", value: "No Language");

        return map;
    }

    /// <summary>
    /// Returns the English display name for an ISO 639 language code (2 or 3
    /// letter). Falls back to the original code uppercased when no match
    /// exists — never throws, never returns null.
    /// </summary>
    public static string EnglishLanguageName(string code)
    {
        if (string.IsNullOrWhiteSpace(value: code))
            return "Unknown";

        return EnglishNameByCode.TryGetValue(key: code, value: out string? name)
            ? name
            : code.ToUpperInvariant();
    }

    private static string StripRegion(string englishName)
    {
        // CultureInfo.EnglishName is "Dutch (Netherlands)" / "English (United States)"
        // for neutral cultures the parenthesis is absent, but defensive trim
        // covers both shapes.
        int paren = englishName.IndexOf(value: ' ', comparisonType: StringComparison.Ordinal);
        if (paren < 0)
            return englishName;

        if (englishName[paren..].StartsWith(value: " (", comparisonType: StringComparison.Ordinal))
            return englishName[..paren];

        return englishName;
    }
}
