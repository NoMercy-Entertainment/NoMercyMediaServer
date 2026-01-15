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
        string tag = !isEnglish && LegacyIsoMap.TryGetValue(iso3, out string? legacyCode)
            ? legacyCode
            : iso3;

        return tag;
    }
}