using Newtonsoft.Json;

namespace NoMercy.Launcher.Models;

public class TraySettings
{
    [JsonProperty("show_on_startup")] public bool ShowOnStartup { get; set; }
    [JsonProperty("startup_arguments")] public string StartupArguments { get; set; } = string.Empty;
}
