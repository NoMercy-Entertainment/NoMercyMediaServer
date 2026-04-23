using Newtonsoft.Json;

namespace NoMercy.Launcher.Models;

public class TraySettings
{
    [JsonProperty("show_on_startup")]
    public bool ShowOnStartup { get; set; }

    [JsonProperty("startup_arguments")]
    public string StartupArguments { get; set; } = string.Empty;

    /// <summary>
    /// When true the server is automatically started when the launcher opens.
    /// The installer update path reads this to decide whether to relaunch
    /// the launcher after a silent install.
    /// </summary>
    [JsonProperty("auto_start")]
    public bool AutoStart { get; set; }
}
