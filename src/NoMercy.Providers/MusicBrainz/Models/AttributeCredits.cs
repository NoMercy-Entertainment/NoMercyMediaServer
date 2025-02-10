using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class AttributeCredits
{
    [JsonProperty("Rhodes piano")] public string RhodesPiano { get; set; } = string.Empty;
    [JsonProperty("synthesizer")] public string Synthesizer { get; set; } = string.Empty;
    [JsonProperty("drums (drum set)")] public string? DrumsDrumSet { get; set; }
    [JsonProperty("handclaps")] public string Handclaps { get; set; } = string.Empty;
    [JsonProperty("Hammond organ")] public string HammondOrgan { get; set; } = string.Empty;
    [JsonProperty("keyboard")] public string Keyboard { get; set; } = string.Empty;
    [JsonProperty("drum machine")] public string DrumMachine { get; set; } = string.Empty;
    [JsonProperty("foot stomps")] public string FootStomps { get; set; } = string.Empty;
    [JsonProperty("Wurlitzer electric piano")] public string WurlitzerElectricPiano { get; set; } = string.Empty;
}