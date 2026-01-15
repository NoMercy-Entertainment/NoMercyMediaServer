using Serilog.Core;
using Serilog.Events;

namespace NoMercy.NmSystem.LogEnrichers;

internal class FileMessageEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.RemovePropertyIfPresent("@mt");
    }
}