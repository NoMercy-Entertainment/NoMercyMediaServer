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
        "MM/dd/yyyy hh:mm:ss tt",  // 12-hour clock with AM/PM
        "MM/dd/yyyy hh:mm tt",     // 12-hour clock with AM/PM, no seconds
        "M/d/yyyy",
        "M/d/yyyy HH:mm:ss",
        "M/d/yyyy hh:mm:ss tt",
        "M/d/yyyy hh:mm tt",
            
        "MM-dd-yyyy",
        "MM-dd-yyyy HH:mm:ss",
        "MM-dd-yyyy hh:mm:ss tt", // 12-hour clock with AM/PM
        "MM-dd-yyyy hh:mm tt",    // 12-hour clock with AM/PM, no seconds
        "M-d-yyyy",
        "M-d-yyyy HH:mm:ss",
        "M-d-yyyy hh:mm:ss tt",
        "M-d-yyyy hh:mm tt"
    ];
    
    private static int _parseYear(DateTime? dateString = null)
    {
        if (string.IsNullOrEmpty(dateString.ToString())) return new DateTime().Year;

        DateTime date;
        try
        {
            date = DateTime.ParseExact(dateString.ToString() ?? string.Empty, "d-M-yyyy HH:mm:ss",
                CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            try
            {
                date = DateTime.ParseExact(dateString.ToString() ?? string.Empty, "d/M/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                date = DateTime.Parse(dateString.ToString() ?? string.Empty, CultureInfo.InvariantCulture);
            }
        }

        return date.Year;
    }

    public static string ToHms(this int seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString();
    }
    
    public static bool TryParseToDateTime(this string value, out DateTime dateTime) {
        return DateTime.TryParseExact(value, ValidFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime) 
               || DateTime.TryParse(value, DateTimeFormatInfo.InvariantInfo, out dateTime)
               && dateTime != default;
    }

    public static DateTime SubDays(this DateTime self, int days)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), "Days must be positive.");
        return self.Subtract(new TimeSpan(days, 0, 0, 0));
    }

    public static int ParseYear(this DateTime? self)
    {
        return string.IsNullOrEmpty(self.ToString())
            ? 0
            : _parseYear(self);
    }

    public static int ParseYear(this DateTime self)
    {
        return string.IsNullOrEmpty(self.ToString(CultureInfo.InvariantCulture))
            ? 0
            : _parseYear(self);
    }

    public static string ToHis(this double time)
    {
        return TimeSpan.FromSeconds(time).ToString(@"hh\:mm\:ss\.fff");
    }

    public static string ToHis(this long time)
    {
        return TimeSpan.FromSeconds(time).ToString(@"hh\:mm\:ss\.fff");
    }
}