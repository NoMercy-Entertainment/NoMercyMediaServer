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
        int result = 0;
        for (int i = strA.Length - 1; i >= 0; i--)
            if (i >= strB.Length || strA[i] == strB[i])
            {
                // Do nothing
            }
            else if (char.ToLower(strA[i]) == char.ToLower(strB[i]))
            {
                result += 1;
            }
            else
            {
                result += 4;
            }

        return 100 - (result + 4 * Math.Abs(strA.Length - strB.Length)) / (2.0 * (strA.Length + strB.Length)) * 100;
    }

    public static List<T> SortByMatchPercentage<T>(IEnumerable<T> array, Func<T, string> keySelector, string match)
    {
        return array.OrderBy(item => MatchPercentage(match, keySelector(item))).ToList();
    }

    public static string RemoveAccents(this string s)
    {
        Encoding destEncoding = Encoding.GetEncoding("ISO-8859-1");

        return destEncoding.GetString(
            Encoding.Convert(Encoding.UTF8, destEncoding, Encoding.UTF8.GetBytes(s)));
    }

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

    public static int ToInt(this string path)
    {
        if (string.IsNullOrEmpty(path)) return 0;
        return int.Parse(path);
    }

    public static int ToInt(this double value)
    {
        return Convert.ToInt32(value);
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
    
    /** This method is used to sanitize a string by removing diacritics, non-alphanumeric characters and accents. */
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
        if (value == null) return "";

        value = Regex.Replace(value, "^The[\\s]*", "");
        value = Regex.Replace(value, "^An[\\s]{1,}", "");
        value = Regex.Replace(value, "^A[\\s]{1,}", "");
        value = Regex.Replace(value, @":\s|\sand\sthe", date != null ? $".{date.ParseYear()}." : ".");
        value = Regex.Replace(value, "\\.", " ");
        value = CleanFileName(value);

        return value.ToLower();
    }

    // public static string TitleSort<T>(this T? self, DateTime? date = null)
    // {
    //     return _parseTitleSort(self?.ToString(), date);
    // }

    private static string _cleanFileName(string? name)
    {
        if (name == null) return "";

        name = Regex.Replace(name, "/", ".");
        name = Regex.Replace(name, ":\\s", ".");
        name = Regex.Replace(name, "\\s", ".");
        name = Regex.Replace(name, "\\? {2}", ".");
        name = Regex.Replace(name, "\\? ", ".");
        name = Regex.Replace(name, "u,\\.", ".");
        name = Regex.Replace(name, "u, ", ".");
        name = Regex.Replace(name, "`", "");
        name = Regex.Replace(name, "'", "");
        name = Regex.Replace(name, "’", "");
        name = Regex.Replace(name, "\"", "");
        name = Regex.Replace(name, "u,", ".");
        name = Regex.Replace(name, "\"", "'");
        name = Regex.Replace(name, "\\s", ".");
        name = Regex.Replace(name, "&", "and");
        name = Regex.Replace(name, "#", ".");
        name = Regex.Replace(name, "!", "");
        name = Regex.Replace(name, "\\*", "-");
        name = Regex.Replace(name, @"\.\.", ".");
        name = Regex.Replace(name, "u,\\.", ".");
        name = Regex.Replace(name, ": ", ".");
        name = Regex.Replace(name, @"\|", ".");
        name = Regex.Replace(name, ":", ".");
        name = Regex.Replace(name, "\\. *$", "");
        name = Regex.Replace(name, @"'|\?|\.\s|-\.|\.\(\d {1,3}\)|[^[:print:]\]|[^-_.\[:alnum:]\]", "");
        name = Regex.Replace(name, "\\. {2,}", ".");

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

    // public static string? TitleCase<T>(this T? self)
    // {
    //     return self?.ToString()?.Split("")[0].ToUpper() + self?.ToString()?.Substring(1).ToLower() ?? "";
    // }

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

        string[] words = str.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
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
}