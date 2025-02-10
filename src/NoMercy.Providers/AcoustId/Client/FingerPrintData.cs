using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Client;
public class FingerPrintData
{
    // ReSharper disable once InconsistentNaming
    [JsonProperty("duration")] public double _duration { get; set; }
    [JsonProperty("fingerprint")] public string Fingerprint { get; set; } = string.Empty;

    public int Duration
    {
        get => (int)Math.Floor(_duration);
        set => _duration = value;
    }
}