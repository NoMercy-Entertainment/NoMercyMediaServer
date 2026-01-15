using Serilog.Core;
using Serilog.Events;

namespace NoMercy.NmSystem.LogEnrichers;

internal class WithThreadIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "ThreadId", Environment.CurrentManagedThreadId));
    }
}