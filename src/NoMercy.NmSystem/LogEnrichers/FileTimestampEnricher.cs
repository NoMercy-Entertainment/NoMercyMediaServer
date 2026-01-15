using Serilog.Core;
using Serilog.Events;

namespace NoMercy.NmSystem.LogEnrichers;

internal class FileTimestampEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        DateTime timestamp = DateTime.UtcNow;

        logEvent.RemovePropertyIfPresent("@t");
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "Time", timestamp));
    }
}