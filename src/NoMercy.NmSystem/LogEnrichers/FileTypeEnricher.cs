using System.Drawing;
using Serilog.Core;
using Serilog.Events;

namespace NoMercy.NmSystem.LogEnrichers;
internal class FileTypeEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.Properties.TryGetValue("ConsoleType", out LogEventPropertyValue? value);

        string type = value?.ToString().Replace("\"", "") ?? "app";

        Color color = Logger.GetColor(type);

        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(
            "Type", type.ToString()));
    }
}