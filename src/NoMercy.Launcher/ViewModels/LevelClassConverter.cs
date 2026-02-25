using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NoMercy.Launcher.ViewModels;

public static class LevelColorConverter
{
    public static FuncValueConverter<string, IBrush> Instance { get; } =
        new(level => level?.ToLowerInvariant() switch
        {
            "fatal" => new(Color.Parse("#DC2626")),
            "error" => new(Color.Parse("#EF4444")),
            "warning" => new(Color.Parse("#EAB308")),
            "debug" => new(Color.Parse("#6B7280")),
            "verbose" => new(Color.Parse("#4B5563")),
            _ => new SolidColorBrush(Color.Parse("#D1D5DB"))
        });
}

public static class LevelWeightConverter
{
    public static FuncValueConverter<string, FontWeight> Instance { get; } =
        new(level => level?.ToLowerInvariant() switch
        {
            "fatal" => FontWeight.Bold,
            "error" => FontWeight.Bold,
            _ => FontWeight.Normal
        });
}

public static class LogColorConverter
{
    private static readonly SolidColorBrush DefaultBrush =
        new(Color.Parse("#D1D5DB"));

    public static FuncValueConverter<string, IBrush> Instance { get; } =
        new(colorHex =>
        {
            if (string.IsNullOrEmpty(colorHex))
                return DefaultBrush;

            try
            {
                return new SolidColorBrush(Color.Parse(colorHex));
            }
            catch
            {
                return DefaultBrush;
            }
        });
}
