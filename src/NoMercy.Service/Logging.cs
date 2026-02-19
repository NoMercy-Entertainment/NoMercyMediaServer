using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Service;

public class CustomLogger<T> : ILogger<T>
{
    private readonly string _categoryName;

    // Add a list of message fragments to filter out
    private static readonly string[] _filteredPhrases =
    [
        "Middleware configuration started",
        "Wildcard detected",
        "Request matched endpoint",
        "The endpoint does not specify",
        "This request accepts compression",
        "Response compression",
        "The response will be compressed",
        "No response compression available",
        "All hosts are allowed",
        "Route pattern:",
        "Found protocol implementation",
        "Executing endpoint",
        "Executed endpoint",
        "Authorization was successful",
        "Authorization failed",
        "Request did not match any endpoints",
        "Microsoft",
    ];

    public CustomLogger()
    {
        _categoryName = typeof(T).Name;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        
        // Filter out specific ASP.NET Core middleware messages
        if (ShouldFilterMessage(message)) return; // Skip logging this message

        LogEventLevel level = ConvertLogLevel(logLevel);

        // Route logs to appropriate category based on the class name
        if (_categoryName.Contains("Queue") || _categoryName.Contains("Cron") || _categoryName.Contains("Job"))
            Logger.Queue($"{message}", level);
        else
            Logger.System($"{message}", level);
    }

    private bool ShouldFilterMessage(string message)
    {
        // Check if the message contains any of the filtered phrases
        return _filteredPhrases.Any(message.Contains);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    private LogEventLevel ConvertLogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}