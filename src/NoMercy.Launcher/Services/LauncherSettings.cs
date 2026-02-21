using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.Launcher.Models;

namespace NoMercy.Launcher.Services;

public static class LauncherSettings
{
    private static string SettingsFile => AppFiles.TraySettingsFile;

    public static TraySettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new();

            string json = File.ReadAllText(SettingsFile);
            return JsonConvert.DeserializeObject<TraySettings>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static void Save(TraySettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsFile);
            if (directory is not null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Ignore write failures
        }
    }
}
