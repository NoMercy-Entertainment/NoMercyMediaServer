using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;
public class ReleaseEvent
{
    [JsonProperty("area")] public MusicBrainzArea MusicBrainzArea { get; set; } = new();

    // ReSharper disable once InconsistentNaming
    [JsonProperty("date")] private string _date { get; set; } = string.Empty;

    [JsonProperty("dateTime")]
    public DateTime? DateTime
    {
        get => !string.IsNullOrWhiteSpace(_date) && !string.IsNullOrEmpty(_date) && _date.TryParseToDateTime(out DateTime dt) ? dt : null;
        set => _date = value.ToString() ?? string.Empty;
    }
}