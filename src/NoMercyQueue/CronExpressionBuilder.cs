// ReSharper disable MemberCanBePrivate.Global
namespace NoMercy.Queue;

public class CronExpressionBuilder
{
    private string _minute = "*";
    private string _hour = "*";
    private string _dayOfMonth = "*";
    private string _month = "*";
    private string _dayOfWeek = "*";

    #region Minute Operations
    public CronExpressionBuilder EveryMinute()
    {
        _minute = "*";
        return this;
    }

    public CronExpressionBuilder EveryMinutes(int minutes)
    {
        if (minutes is < 1 or > 59)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be between 1 and 59");
        _minute = $"*/{minutes}";
        return this;
    }

    public CronExpressionBuilder AtMinute(int minute)
    {
        if (minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
        _minute = minute.ToString();
        return this;
    }

    public CronExpressionBuilder AtMinutes(params int[] minutes)
    {
        if (minutes.Any(minute => minute is < 0 or > 59))
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be between 0 and 59");
        }
        _minute = string.Join(",", minutes);
        return this;
    }

    public CronExpressionBuilder MinuteRange(int start, int end)
    {
        if (start < 0 || start > 59 || end < 0 || end > 59 || start >= end)
            throw new ArgumentException("Invalid minute range");
        _minute = $"{start}-{end}";
        return this;
    }
    #endregion

    #region Hour Operations
    public CronExpressionBuilder EveryHour()
    {
        _minute = "0";
        _hour = "*";
        return this;
    }

    public CronExpressionBuilder EveryHours(int hours)
    {
        if (hours is < 1 or > 23)
            throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be between 1 and 23");
        _minute = "0";
        _hour = $"*/{hours}";
        return this;
    }

    public CronExpressionBuilder AtHour(int hour)
    {
        if (hour is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        _minute = "0";
        _hour = hour.ToString();
        return this;
    }

    public CronExpressionBuilder AtHours(params int[] hours)
    {
        if (hours.Any(hour => hour is < 0 or > 23))
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be between 0 and 23");
        }
        _minute = "0";
        _hour = string.Join(",", hours);
        return this;
    }

    public CronExpressionBuilder HourRange(int start, int end)
    {
        if (start < 0 || start > 23 || end < 0 || end > 23 || start >= end)
            throw new ArgumentException("Invalid hour range");
        _minute = "0";
        _hour = $"{start}-{end}";
        return this;
    }
    #endregion

    #region Day of Month Operations
    public CronExpressionBuilder EveryDay()
    {
        _dayOfMonth = "*";
        return this;
    }

    public CronExpressionBuilder OnDay(int day)
    {
        if (day is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(day), "Day must be between 1 and 31");
        _dayOfMonth = day.ToString();
        return this;
    }

    public CronExpressionBuilder OnDays(params int[] days)
    {
        foreach (int day in days)
        {
            if (day is < 1 or > 31)
                throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 31");
        }
        _dayOfMonth = string.Join(",", days);
        return this;
    }

    public CronExpressionBuilder DayRange(int start, int end)
    {
        if (start < 1 || start > 31 || end < 1 || end > 31 || start >= end)
            throw new ArgumentException("Invalid day range");
        _dayOfMonth = $"{start}-{end}";
        return this;
    }

    public CronExpressionBuilder EveryNthDay(int n)
    {
        if (n is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(n), "N must be between 1 and 31");
        _minute = "0";
        _hour = "0";
        _dayOfMonth = $"*/{n}";
        return this;
    }

    public CronExpressionBuilder LastDayOfMonth()
    {
        _minute = "0";
        _hour = "0";
        _dayOfMonth = "L";
        return this;
    }
    #endregion

    #region Month Operations
    public CronExpressionBuilder EveryMonth()
    {
        _month = "*";
        return this;
    }

    public CronExpressionBuilder InMonth(int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12");
        _month = month.ToString();
        return this;
    }

    public CronExpressionBuilder InMonths(params int[] months)
    {
        foreach (int month in months)
        {
            if (month is < 1 or > 12)
                throw new ArgumentOutOfRangeException(nameof(months), "Months must be between 1 and 12");
        }
        _month = string.Join(",", months);
        return this;
    }

    public CronExpressionBuilder MonthRange(int start, int end)
    {
        if (start < 1 || start > 12 || end < 1 || end > 12 || start >= end)
            throw new ArgumentException("Invalid month range");
        _month = $"{start}-{end}";
        return this;
    }

    public CronExpressionBuilder EveryNthMonth(int n)
    {
        if (n is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(n), "N must be between 1 and 12");
        _minute = "0";
        _hour = "0";
        _dayOfMonth = "1";
        _month = $"*/{n}";
        return this;
    }
    #endregion

    #region Day of Week Operations
    public CronExpressionBuilder AnyDayOfWeek()
    {
        _dayOfWeek = "*";
        return this;
    }

    public CronExpressionBuilder OnDayOfWeek(DayOfWeek dayOfWeek)
    {
        _dayOfWeek = ((int)dayOfWeek).ToString();
        return this;
    }

    public CronExpressionBuilder OnDaysOfWeek(params DayOfWeek[] daysOfWeek)
    {
        int[] days = daysOfWeek.Select(d => (int)d).ToArray();
        _dayOfWeek = string.Join(",", days);
        return this;
    }

    public CronExpressionBuilder Weekdays()
    {
        _minute = "0";
        _hour = "0";
        _dayOfMonth = "*";
        _dayOfWeek = "1-5";
        return this;
    }

    public CronExpressionBuilder Weekends()
    {
        _minute = "0";
        _hour = "0";
        _dayOfMonth = "*";
        _dayOfWeek = "0,6";
        return this;
    }

    public CronExpressionBuilder OnNthDayOfWeek(int nth, DayOfWeek dayOfWeek)
    {
        if (nth is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(nth), "Nth must be between 1 and 5");
        _minute = "0";
        _hour = "0";
        _dayOfMonth = "*";
        _dayOfWeek = $"{(int)dayOfWeek}#{nth}";
        return this;
    }

    public CronExpressionBuilder LastDayOfWeek(DayOfWeek dayOfWeek)
    {
        _minute = "0";
        _hour = "0";
        _dayOfMonth = "*";
        _dayOfWeek = $"{(int)dayOfWeek}L";
        return this;
    }
    #endregion

    #region Common Patterns
    public CronExpressionBuilder Daily(int hour = 0, int minute = 0)
    {
        if (hour is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        if (minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
        
        _minute = minute.ToString();
        _hour = hour.ToString();
        _dayOfMonth = "*";
        _month = "*";
        _dayOfWeek = "*";
        return this;
    }

    public CronExpressionBuilder Weekly(DayOfWeek dayOfWeek, int hour = 0, int minute = 0)
    {
        if (hour is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        if (minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
        
        _minute = minute.ToString();
        _hour = hour.ToString();
        _dayOfMonth = "*";
        _month = "*";
        _dayOfWeek = ((int)dayOfWeek).ToString();
        return this;
    }

    public CronExpressionBuilder Monthly(int day, int hour = 0, int minute = 0)
    {
        if (day is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(day), "Day must be between 1 and 31");
        if (hour is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        if (minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
        
        _minute = minute.ToString();
        _hour = hour.ToString();
        _dayOfMonth = day.ToString();
        _month = "*";
        _dayOfWeek = "*";
        return this;
    }

    public CronExpressionBuilder Yearly(int month, int day, int hour = 0, int minute = 0)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12");
        if (day is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(day), "Day must be between 1 and 31");
        if (hour is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
        if (minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
        
        _minute = minute.ToString();
        _hour = hour.ToString();
        _dayOfMonth = day.ToString();
        _month = month.ToString();
        _dayOfWeek = "*";
        return this;
    }

    public CronExpressionBuilder Hourly(int minute = 0)
    {
        if (minute is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
        
        _minute = minute.ToString();
        _hour = "*";
        _dayOfMonth = "*";
        _month = "*";
        _dayOfWeek = "*";
        return this;
    }
    #endregion

    #region Builder Pattern
    public string Build() => $"{_minute} {_hour} {_dayOfMonth} {_month} {_dayOfWeek}";

    public override string ToString() => Build();

    public static implicit operator string(CronExpressionBuilder builder) => builder.Build();

    public CronExpressionBuilder Reset()
    {
        _minute = "*";
        _hour = "*";
        _dayOfMonth = "*";
        _month = "*";
        _dayOfWeek = "*";
        return this;
    }
    #endregion
}