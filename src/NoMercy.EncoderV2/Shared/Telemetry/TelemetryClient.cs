namespace NoMercy.EncoderV2.Shared.Telemetry;

/// <summary>
/// Sends telemetry/events from encoder to configured sinks
/// </summary>
public interface ITelemetryClient
{
    void TrackEvent(string eventName, Dictionary<string, string>? properties = null);
    void TrackMetric(string metricName, double value, Dictionary<string, string>? properties = null);
    void TrackException(Exception exception, Dictionary<string, string>? properties = null);
    void TrackDependency(string dependencyName, string dependencyType, TimeSpan duration, bool success);
}

public class TelemetryEvent
{
    public string EventName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Properties { get; set; } = [];
}

public class TelemetryMetric
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Properties { get; set; } = [];
}

public class TelemetryClient : ITelemetryClient
{
    private readonly List<TelemetryEvent> _events = [];
    private readonly List<TelemetryMetric> _metrics = [];

    public void TrackEvent(string eventName, Dictionary<string, string>? properties = null)
    {
        TelemetryEvent telemetryEvent = new()
        {
            EventName = eventName,
            Properties = properties ?? []
        };

        _events.Add(telemetryEvent);
        Console.WriteLine($"[Telemetry] Event: {eventName}");
    }

    public void TrackMetric(string metricName, double value, Dictionary<string, string>? properties = null)
    {
        TelemetryMetric metric = new()
        {
            MetricName = metricName,
            Value = value,
            Properties = properties ?? []
        };

        _metrics.Add(metric);
        Console.WriteLine($"[Telemetry] Metric: {metricName} = {value}");
    }

    public void TrackException(Exception exception, Dictionary<string, string>? properties = null)
    {
        Dictionary<string, string> props = properties ?? [];
        props["ExceptionType"] = exception.GetType().Name;
        props["ExceptionMessage"] = exception.Message;

        TelemetryEvent telemetryEvent = new()
        {
            EventName = "Exception",
            Properties = props
        };

        _events.Add(telemetryEvent);
        Console.WriteLine($"[Telemetry] Exception: {exception.GetType().Name} - {exception.Message}");
    }

    public void TrackDependency(string dependencyName, string dependencyType, TimeSpan duration, bool success)
    {
        Dictionary<string, string> props = new()
        {
            ["DependencyType"] = dependencyType,
            ["Duration"] = duration.TotalMilliseconds.ToString("F2"),
            ["Success"] = success.ToString()
        };

        TelemetryEvent telemetryEvent = new()
        {
            EventName = $"Dependency_{dependencyName}",
            Properties = props
        };

        _events.Add(telemetryEvent);
        Console.WriteLine($"[Telemetry] Dependency: {dependencyName} ({dependencyType}) - {duration.TotalMilliseconds:F2}ms - Success: {success}");
    }
}

