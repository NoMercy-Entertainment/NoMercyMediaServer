using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.NmSystem.Extensions;

public static partial class Str
{
    public static string DirectorySeparator => Path.DirectorySeparatorChar.ToString();
    
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
        int[,] dp = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            dp[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            dp[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(
                        dp[i - 1, j] + 1,    // Deletion
                        dp[i, j - 1] + 1),   // Insertion
                    dp[i - 1, j - 1] + cost); // Substitution
            }
        }

        return dp[s1.Length, s2.Length];
    }
    
    public static List<T> SortByMatchPercentage<T>(IEnumerable<T> array, Func<T, string> keySelector, string match) where T : class
    {
        return array.OrderBy(item => MatchPercentage(match, keySelector(item))).ToList();
    }
    
    public static List<T> ToSortByMatchPercentage<T>(this IEnumerable<T> array, Func<T, string> keySelector, string match) where T : class
    {
        return array.OrderBy(item => MatchPercentage(match, keySelector(item))).ToList();
    }

    [Pure]
    public static string RemoveAccents(this string s)
    {
        Encoding destEncoding = Encoding.GetEncoding("ISO-8859-1");

        return destEncoding.GetString(
            Encoding.Convert(Encoding.UTF8, destEncoding, Encoding.UTF8.GetBytes(s)));
    }

    [Pure]
    public static string RemoveDiacritics(this string text)
    {
        string formD = text.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new();

        foreach (char ch in formD)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string RemoveNonAlphaNumericCharacters(this string text)
    {
        return Regex.Replace(text, @"[^a-zA-Z0-9\s.-]", "");
    }

    [GeneratedRegex(@"(1(8|9)|20)\d{2}(?!p|i|(1(8|9)|20)\d{2}|\W(1(8|9)|20)\d{2})")]
    public static partial Regex MatchYearRegex();

    public static string PathName(this string path)
    {
        return Regex.Replace(path, @"[\/\\]", DirectorySeparator);
    }

    public static int ToInt(this string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        return (int)Math.Round(double.Parse(value, CultureInfo.InvariantCulture));
    }

    public static int ToInt(this double value)
    {
        return Convert.ToInt32(value);
    }

    public static double ToDouble(this string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    public static double ToDouble(this int value)
    {
        return Convert.ToDouble(value);
    }
    
    public static bool ToBoolean(this string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return bool.Parse(value);
    }

    public static string Spacer(string text, int padding, bool begin = false)
    {
        return begin ? SpacerBegin(text, padding) : SpacerEnd(text, padding);
    }

    private static string SpacerEnd(string text, int padding)
    {
        StringBuilder spacing = new();
        spacing.Append(text);
        for (int i = 0; i < padding - text.Length; i++) spacing.Append(' ');

        return spacing.ToString();
    }

    private static string SpacerBegin(string text, int padding)
    {
        StringBuilder spacing = new();
        for (int i = 0; i < padding - text.Length; i++) spacing.Append(' ');
        spacing.Append(text);

        return spacing.ToString();
    }

    public static string ToHexString(this Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static string ToHexString(this Rgb24 color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static Guid ToGuid(this string id)
    {
        return Guid.Parse(id);
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
        return str.RemoveDiacritics().RemoveNonAlphaNumericCharacters().RemoveAccents();
    }
    
    public static bool ContainsSanitized(this string str, string value)
    {
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
        return str + ((parameters is not null && parameters.Count > 0) ? "?" + string.Join("&", parameters.Select(pair => $"{pair.Key}={pair.Value}")) : string.Empty);
    }
    
    public static string EscapeQuotes(this string str)
    {
        return Regex.Replace(str, "\"", $"'");
    }
    
    private static string _parseTitleSort(string? value = null, DateTime? date = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        // Remove leading "The ", "An ", "A " (case-insensitive)
        value = Regex.Replace(value, @"^(The|An|A)\s+", "", RegexOptions.IgnoreCase);

        // Replace ": " and " and the " with the year if available
        if (date != null)
        {
            string year = date.Value.Year.ToString();
            value = Regex.Replace(value, @"[:]\s| and the ", $".{year}.", RegexOptions.IgnoreCase);
        }

        // Replace multiple dots with a space (keeps readability)
        value = Regex.Replace(value, @"\.+", " ");

        // Sanitize file name to remove unwanted characters
        value = CleanFileName(value);

        return value.ToLower().Trim();
    }

    private static string _cleanFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        // Replace forbidden characters with a dot or remove them
        name = Regex.Replace(name, @"[/\\|:*?\""<>{}]", ".");
        // Remove unwanted punctuation
        name = Regex.Replace(name, @"[!'’`]", "");
        // Replace ampersand
        name = Regex.Replace(name, @"&", "and");
        // Replace spaces with dots
        name = Regex.Replace(name, @"\s+", ".");
        // Collapse multiple dots
        name = Regex.Replace(name, @"\.+", ".");
        // Trim leading/trailing dots
        name = Regex.Replace(name, @"^\.+|\.+$", "");

        return name.Trim();
    }

    public static string CleanFileName(this string? self)
    {
        return _cleanFileName(self);
    }

    public static string TitleSort(this object self, int? parseYear)
    {
        return _parseTitleSort(self.ToString(), parseYear != null ? new DateTime(parseYear.Value, 1, 1) : null);
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

    public static string ToUcFirst(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return char.ToUpper(str[0]) + str[1..].ToLower();
    }

    public static int ToSeconds(this string hms)
    {
        if (string.IsNullOrEmpty(hms)) return 0;

        int[] parts = hms.Split(':').Select(int.Parse).ToArray();
        if (parts.Length < 3) parts = new[] { 0 }.Concat(parts).ToArray();

        return parts[0] * 60 * 60 + parts[1] * 60 + parts[2];
    }

    public static string TitleSort<T>(this T? self, DateTime? date = null)
    {
        return _parseTitleSort(self?.ToString(), date);
    }
    
    public static string ToName(this string str)
    {;
        return NumberConverter.ConvertNumbersInString(str);
    }
}