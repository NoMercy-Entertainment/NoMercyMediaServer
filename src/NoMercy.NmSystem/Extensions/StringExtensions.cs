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

public static partial class StringExtensions
{
    public static string DirectorySeparator => Path.DirectorySeparatorChar.ToString();

    [Pure]
    public static string RemoveAccents(this string s)
    {
        Encoding destEncoding = Encoding.GetEncoding("ISO-8859-1");

        return destEncoding.GetString(
            Encoding.Convert(Encoding.UTF8, destEncoding, Encoding.UTF8.GetBytes(s))
        );
    }

    [Pure]
    public static string RemoveDiacritics(this string text)
    {
        string formD = text.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new();

        foreach (char ch in formD)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string RemoveNonAlphaNumericCharacters(this string text)
    {
        return Regex.Replace(text, @"[^a-zA-Z0-9\s.-]", "");
    }

    [GeneratedRegex(@"(1(8|9)|20)\d{2}(?!p|i|(1(8|9)|20)\d{2}|\W(1(8|9)|20)\d{2})")]
    public static partial Regex MatchYearRegex();

    public static string? TryGetYear(this string str)
    {
        Match match = MatchYearRegex().Match(str);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"\[.*?\]")]
    public static partial Regex RemoveBracketedString();

    /// <summary>Matches a [tmdb-1234] hint embedded in a filename.</summary>
    [GeneratedRegex(@"\[tmdb-(\d+)\]", RegexOptions.IgnoreCase)]
    public static partial Regex MatchTmdbHint();

    /// <summary>
    /// Extracts the TMDB ID embedded as <c>[tmdb-1234]</c> anywhere in the string,
    /// or returns <see langword="null"/> if no hint is present.
    /// </summary>
    public static int? TryGetTmdbHint(this string str)
    {
        Match m = MatchTmdbHint().Match(str);
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"\d+")]
    public static partial Regex MatchNumbers();

    [GeneratedRegex(@"[\.\s\-_]S\d{1,2}(?:E\d+)?(?:[\.\s\-_]|$)", RegexOptions.IgnoreCase)]
    public static partial Regex MatchSeasonTag();

    [GeneratedRegex(@"^S(\d{1,2})E(\d+)", RegexOptions.IgnoreCase)]
    public static partial Regex MatchEpisodePrefix();

    [GeneratedRegex(@"[\.\s\-_]S(\d{1,2})E(\d+)", RegexOptions.IgnoreCase)]
    public static partial Regex MatchSeasonEpisode();

    [GeneratedRegex(@"\(.*?\)")]
    public static partial Regex RemoveParenthesizedString();

    [GeneratedRegex(@"[\-\.\s]+Episode\s*(\d+)", RegexOptions.IgnoreCase)]
    public static partial Regex MatchEpisodeWord();

    [GeneratedRegex(@"(?<![A-Za-z0-9])(\d{1,2})[xX×](\d{1,3})(?![0-9])")]
    public static partial Regex MatchCrossFormatEpisode();

    /// <summary>
    /// Vocabulary follows the scene rules (scenerules.org); cf. the MIT-licensed
    /// pr0pz/scene-release-parser for the canonical token tables.
    /// Matches the first scene "quality/source/codec/audio/flag" tag in a release
    /// name. Tokens are bounded by non-alphanumeric characters so real title words
    /// that merely contain a token as a substring (e.g. "Limitless", "Web Therapy")
    /// are never matched, and internal separators are tolerated so dotted/hyphenated
    /// scene forms ("WEB-DL", "H.264", "DDP5.1") match the same as spaced forms.
    /// </summary>
    [GeneratedRegex(
        @"(?<![A-Za-z0-9])(?:" +
        @"\d{3,4}[pi]|[48]k|uhd|" +
        @"web[\s.\-]?dl|web[\s.\-]?rip|web[\s.\-]?hd|blu[\s.\-]?ray|bd[\s.\-]?rip|b[rd][\s.\-]?rip|hd[\s.\-]?tv|pd[\s.\-]?tv|dvd[\s.\-]?rip|hd[\s.\-]?rip|hd[\s.\-]?cam|uhd[\s.\-]?bd|remux|" +
        @"x[\s.\-]?26[45]|h[\s.\-]?26[45]|hevc|xvid|divx|" +
        @"ddp?[\s.\-]?\d(?:[\s.\-]?\d)?|e?ac[\s.\-]?3|dts(?:[\s.\-]?hd)?(?:[\s.\-]?ma)?|true[\s.\-]?hd|atmos|aac(?:[\s.\-]?\d(?:[\s.\-]?\d)?)?|flac|" +
        @"\d{1,2}[\s.\-]?bit|hdr10\+?|hdr|do[\s.\-]?vi|dolby[\s.\-]?vision|" +
        @"hd[\s.\-]?dvd|hd[\s.\-]?tc|ed[\s.\-]?tv|dvd[\s.\-]?scr|dvdscr|dsr|amzn|dsnp|atvp|hmax|hulu|nflx|avc|vc[\s.\-]?1|wmv|mpeg2?|vp[89]|ac3d|eac3d|dts[\s.\-]?(?:es|x)|mp3|hlg|hd[\s.\-]?light|imax|" +
        @"repack|multi" +
        @")(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase)]
    public static partial Regex MatchReleaseTag();

    /// <summary>
    /// Cleans a derived show/movie title by normalizing separators and removing
    /// everything from the first recognized scene tag onward (resolution, source,
    /// codec, audio, HDR and release flags). Year extraction is intentionally left
    /// to the caller. Returns the trimmed leading portion of the title.
    /// </summary>
    [Pure]
    public static string CleanReleaseTitle(this string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string normalized = raw.Replace('.', ' ').Replace('_', ' ');
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        Match tag = MatchReleaseTag().Match(normalized);
        string title = tag.Success ? normalized[..tag.Index] : normalized;

        title = title.TrimEnd(' ', '-', '_', '.');
        return Regex.Replace(title, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Series-aware title cleanup: applies <see cref="CleanReleaseTitle"/> and then
    /// drops a trailing release year (e.g. "Halo 2022" -> "Halo", "New Amsterdam
    /// 2018" -> "New Amsterdam"). A year at the very start is kept so shows that are
    /// literally named by a year ("1883", "1923", "1899") survive intact.
    /// </summary>
    [Pure]
    public static string CleanSeriesTitle(this string raw)
    {
        string title = raw.CleanReleaseTitle();

        Match year = MatchYearRegex().Match(title);
        if (year.Success && year.Index > 0)
            title = title[..year.Index];

        return title.TrimEnd('-', '.', '_', ' ').Trim();
    }

    /// <summary>Matches a "Season N" / "Series N" (and common localized) folder name.</summary>
    [GeneratedRegex(
        @"(?<![A-Za-z])(?:season|series|saison|staffel|temporada|stagione)[\s\.\-_]*0*(\d{1,3})(?![0-9])",
        RegexOptions.IgnoreCase)]
    public static partial Regex MatchFolderSeasonWord();

    /// <summary>Matches a bare "S2" / "S02" folder name (whole string).</summary>
    [GeneratedRegex(@"^[Ss]0*(\d{1,3})$")]
    public static partial Regex MatchSeasonFolderShort();

    /// <summary>
    /// Extracts a season number from a directory path's leaf folder, e.g.
    /// "/media/Show/Season 02" -> 2, "Show/S2" -> 2, "X/Season 0" -> 0. Returns
    /// <see langword="null"/> when the folder is not a season folder, so a flat
    /// show folder never spuriously supplies a season.
    /// </summary>
    public static int? TryGetFolderSeason(this string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        string folder = Path.GetFileName(directory.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(folder))
            folder = directory;

        Match word = MatchFolderSeasonWord().Match(folder);
        if (word.Success)
            return int.Parse(word.Groups[1].Value);

        Match shortForm = MatchSeasonFolderShort().Match(folder);
        if (shortForm.Success)
            return int.Parse(shortForm.Groups[1].Value);

        return null;
    }


    [GeneratedRegex("/[^a-zA-Z0-9]/")]
    public static partial Regex IsAlphaNumeric();

    public static bool IsAlphaNumeric(this string str)
    {
        return IsAlphaNumeric().IsMatch(str);
    }

    [GeneratedRegex("/[0-9]/")]
    public static partial Regex IsNumeric();

    public static bool IsNumeric(this string str)
    {
        return IsNumeric().IsMatch(str);
    }

    public static string PathName(this string path)
    {
        return Regex.Replace(path, @"[\/\\]", DirectorySeparator);
    }

    public static int ToInt(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double d
        )
            ? (int)Math.Round(d)
            : 0;
    }

    public static int ToInt(this double value)
    {
        return Convert.ToInt32(value);
    }

    public static int ToInt(this uint value)
    {
        return Convert.ToInt32(value);
    }

    public static double ToDouble(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double d
        )
            ? d
            : 0;
    }

    public static double ToDouble(this int value)
    {
        return Convert.ToDouble(value);
    }

    public static long ToLong(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)
            ? l
            : 0;
    }

    public static bool ToBoolean(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        return bool.TryParse(value, out bool b) && b;
    }

    public static string Spacer(string text, int padding, bool begin = false)
    {
        return begin ? SpacerBegin(text, padding) : SpacerEnd(text, padding);
    }

    private static string SpacerEnd(string text, int padding)
    {
        StringBuilder spacing = new();
        spacing.Append(text);
        for (int i = 0; i < padding - text.Length; i++)
            spacing.Append(' ');

        return spacing.ToString();
    }

    private static string SpacerBegin(string text, int padding)
    {
        StringBuilder spacing = new();
        for (int i = 0; i < padding - text.Length; i++)
            spacing.Append(' ');
        spacing.Append(text);

        return spacing.ToString();
    }

    /// <summary>
    /// Parses a Guid string. Returns <see cref="Guid.Empty"/> when the input
    /// cannot be parsed so call sites that previously crashed on a malformed
    /// id surface as a 'no match' instead. Callers that need strict
    /// validation should use <see cref="Guid.TryParse(string?, out Guid)"/>
    /// directly.
    /// </summary>
    public static Guid ToGuid(this string? id)
    {
        return Guid.TryParse(id, out Guid parsed) ? parsed : Guid.Empty;
    }

    public static string ToUtf8(this string value)
    {
        return Encoding.UTF8.GetString(Encoding.Default.GetBytes(value));
    }

    public static string SplitPascalCase(this string str)
    {
        str = Regex.Replace(str, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2");
        return Regex.Replace(str, @"(\p{Ll})(\P{Ll})", "$1 $2");
    }

    /** This method sanitizes a string by removing diacritics, non-alphanumeric characters and accents. */
    public static string Sanitize(this string str)
    {
        return str.RemoveDiacritics().RemoveNonAlphaNumericCharacters().RemoveAccents().Trim();
    }

    public static bool ContainsSanitized(this string str, string? value)
    {
        if (value == null)
            return false;

        str = str.Sanitize().ToLower();
        value = value.Sanitize().ToLower();
        return str.Contains(value) || value.Contains(str);
    }

    public static bool EqualsSanitized(this string str, string value)
    {
        str = str.Sanitize().ToLower();
        value = value.Sanitize().ToLower();
        return str.Equals(value) || value.Equals(str);
    }

    public static string UrlDecode(this string str)
    {
        return WebUtility.UrlDecode(str);
    }

    public static string UrlEncode(this string str)
    {
        return WebUtility.UrlEncode(str);
    }

    public static string ToQueryUri(this string str, Dictionary<string, string>? parameters)
    {
        return str
            + (
                parameters is not null && parameters.Count > 0
                    ? "?" + string.Join("&", parameters.Select(pair => $"{pair.Key}={pair.Value}"))
                    : string.Empty
            );
    }

    public static string EscapeQuotes(this string str)
    {
        return Regex.Replace(str, "\"", "'");
    }

    public static string Capitalize(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return char.ToUpper(str[0]) + str.Substring(1);
    }

    public static string ToTitleCase(this string str, string culture = "en-US")
    {
        if (string.IsNullOrEmpty(str))
            return str;

        TextInfo textInfo = new CultureInfo(culture, false).TextInfo;
        return textInfo.ToTitleCase(str.ToLower());
    }

    public static string ToPascalCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        string[] words = str.Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join("_", words.Select(word => word[..1].ToUpper() + word[1..].ToLower()));
    }

    public static string ToSnakeCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        StringBuilder sb = new();
        for (int i = 0; i < str.Length; i++)
        {
            if (char.IsUpper(str[i]) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLower(str[i]));
        }
        return sb.ToString();
    }

    public static string ToUcFirst(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return char.ToUpper(str[0]) + str[1..].ToLower();
    }

    public static int ToSeconds(this string? hms)
    {
        if (string.IsNullOrEmpty(hms))
            return 0;

        int[] parts = hms.Split('.').ElementAt(0).Split(':').Select(int.Parse).ToArray();
        if (parts.Length < 3)
            parts = new[] { 0 }.Concat(parts).ToArray();

        return parts[0] * 60 * 60 + parts[1] * 60 + parts[2];
    }

    public static int ToSeconds(this double hms)
    {
        return (int)Math.Round(hms);
    }

    public static int ToMilliSeconds(this string? hms)
    {
        return hms.ToSeconds() * 1000;
    }

    public static string ToName(this string str)
    {
        ;
        return NumberConverter.ConvertNumbersInString(str);
    }

    public static string NormalizeSearch(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Normalize to FormD to separate characters and diacritics
        string normalized = input.Normalize(NormalizationForm.FormD);

        // Remove diacritics and normalize dashes in a single pass
        StringBuilder stringBuilder = new(normalized.Length);
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            char appended = c switch
            {
                '\u2010' => '-', // Hyphen
                '\u2013' => '-', // En dash
                '\u2014' => '-', // Em dash
                '\u2212' => '-', // Minus sign
                _ => c,
            };
            stringBuilder.Append(char.ToLowerInvariant(appended));
        }

        string result = stringBuilder.ToString();

        // Remove non-alphanumeric characters (optional)
        result = Regex.Replace(result, @"[^a-zA-Z0-9\s-]", "");

        return result;
    }

    public enum TextDirection
    {
        LTR,
        RTL,
    }

    public static TextDirection GetTextDirection(this string str)
    {
        // Check for presence of RTL characters
        foreach (char c in str)
        {
            if (
                (c >= '\u0590' && c <= '\u05FF')
                || // Hebrew
                (c >= '\u0600' && c <= '\u06FF')
                || // Arabic
                (c >= '\u0750' && c <= '\u077F')
                || // Arabic Supplement
                (c >= '\u08A0' && c <= '\u08FF')
                || // Arabic Extended-A
                (c >= '\uFB50' && c <= '\uFDFF')
                || // Arabic Presentation Forms-A
                (c >= '\uFE70' && c <= '\uFEFF')
            ) // Arabic Presentation Forms-B
            {
                return TextDirection.RTL;
            }
        }
        return TextDirection.LTR;
    }

    public static string ToSlug(this string value)
    {
        //First to lower case
        value = value.ToLowerInvariant();

        //Remove all accents
        byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(value);
        value = Encoding.ASCII.GetString(bytes);

        //Replace spaces
        value = Regex.Replace(value, @"\s", "-", RegexOptions.Compiled);

        //Remove invalid chars
        value = Regex.Replace(value, @"[^a-z0-9\s-_]", "", RegexOptions.Compiled);

        //Trim dashes from end
        value = value.Trim('-', '_');

        //Replace double occurences of - or _
        value = Regex.Replace(value, @"([-_]){2,}", "$1", RegexOptions.Compiled);

        // random id
        value += "-" + Guid.NewGuid().ToString("n").Substring(0, 8);

        return value;
    }
}
