using Newtonsoft.Json;

namespace NoMercy.Tray.Models;

public class TraySettings
{
    [JsonProperty("show_on_startup")] public bool ShowOnStartup { get; set; }
}
