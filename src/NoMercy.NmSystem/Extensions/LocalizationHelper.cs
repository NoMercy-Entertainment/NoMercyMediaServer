using System.Reflection;
using System.Xml.Linq;
using I18N.DotNet;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.Extensions;

public static class LocalizationHelper
{
    public static ILocalizer GlobalLocalizer { get; set; } = new Localizer();

    // Single-writer lock + in-process dedup so two concurrent requests can't
    // race on the I18N.xml file handle and so we don't repeatedly load+save
    // the document for the same missing key while a previous append is mid-write.
    private static readonly object WriteLock = new();
    private static readonly HashSet<string> SeenMissingKeys = new(StringComparer.Ordinal);

    public static string Localize(this string key)
    {
        string localized = GlobalLocalizer.Localize(key);
        if (key == localized && Config.IsDev)
        {
            TryAppendMissingLocalization(key);
            return key;
        }

        return localized;
    }

    private static void TryAppendMissingLocalization(string key)
    {
        // A best-effort dev-only convenience must NEVER break a controller
        // response. Localize() is called from DTO setters; an IOException
        // here was bubbling all the way back to the request pipeline and
        // returning 500 to the dashboard.
        try
        {
            AppendMissingLocalization(key);
        }
        catch (Exception ex)
        {
            Logger.App(
                $"LocalizationHelper: failed to record missing key '{key}': {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }

    private static void AppendMissingLocalization(string key)
    {
        // Cheap pre-check outside the lock — vast majority of calls are
        // repeat hits for the same handful of keys during a session.
        lock (SeenMissingKeys)
        {
            if (!SeenMissingKeys.Add(key))
                return;
        }

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string? projectRoot = Directory
            .GetParent(baseDirectory)
            ?.Parent?.Parent?.Parent?.Parent?.FullName;
        if (projectRoot is null)
            return;

        string filePath = Path.Combine(projectRoot, "NoMercy.Api", "Resources", "I18N.xml");

        lock (WriteLock)
        {
            XDocument doc = XDocument.Load(filePath);

            // Re-check inside the lock against the on-disk file — a previous
            // session may have added the key already.
            bool exists =
                doc.Root?.Elements("Entry").Any(e => e.Element("Key")?.Value == key) == true;
            if (exists)
                return;

            XElement newEntry = new(
                "Entry",
                new XElement("Key", key),
                new XElement("Value", new XAttribute("lang", "nl"), key)
            );

            doc.Root?.Add(newEntry);
            doc.Save(filePath);

            // Reload the localizer to include the new entry
            Localizer reportLocalizer = new();
            reportLocalizer.LoadXML(Assembly.GetExecutingAssembly(), "Resources.I18N.xml", "nl");
            GlobalLocalizer = reportLocalizer;
        }
    }
}
