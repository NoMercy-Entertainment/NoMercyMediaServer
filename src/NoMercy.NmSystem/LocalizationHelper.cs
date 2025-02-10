using System.Reflection;
using System.Xml.Linq;
using I18N.DotNet;

namespace NoMercy.NmSystem;

public static class LocalizationHelper
{
    public static ILocalizer GlobalLocalizer { get; set; } = new Localizer();

    public static string Localize(this string key)
    {
        string localized = GlobalLocalizer.Localize(key);
        if (key == localized && Config.IsDev)
        {
            AppendMissingLocalization(key);
            return key;
        }
        return localized;
    }

    private static void AppendMissingLocalization(string key)
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string? projectRoot = Directory.GetParent(baseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName;
        if (projectRoot is null) return;

        string filePath = Path.Combine(projectRoot, "NoMercy.Api", "Resources", "I18N.xml");
        XDocument doc = XDocument.Load(filePath);

        // Check if the key already exists to avoid duplicates
        bool exists = doc.Root
            ?.Elements("Entry")
            .Any(e => e.Element("Key")?.Value == key) == true;
        if (exists) return;

        XElement newEntry = new("Entry",
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
