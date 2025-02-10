
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzRelease
{
    [JsonProperty("barcode")] public string Barcode { get; set; } = string.Empty;
    [JsonProperty("country")] public string Country { get; set; } = string.Empty;
    [JsonProperty("score")] public int? Score { get; set; }

    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }

    [JsonProperty("genres")] public MusicBrainzGenreDetails[] Genres { get; set; } = [];
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("media")] public MusicBrainzMedia[] Media { get; set; } = [];
    [JsonProperty("packaging")] public string Packaging { get; set; } = string.Empty;
    [JsonProperty("packaging-id")] public Guid? PackagingId { get; set; }
    [JsonProperty("quality")] public string Quality { get; set; } = string.Empty;
    [JsonProperty("release-events")] public ReleaseEvent[]? ReleaseEvents { get; set; } = [];
    [JsonProperty("release-group")] public MusicBrainzReleaseGroup MusicBrainzReleaseGroup { get; set; } = new();
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("status-id")] public Guid? StatusId { get; set; }

    [JsonProperty("artist-credit")] public ReleaseArtistCredit[] ArtistCredit { get; set; } = [];

    [JsonProperty("text-representation")]
    public MusicBrainzTextRepresentation MusicBrainzTextRepresentation { get; set; } = new();

    [JsonProperty("title")] public string Title { get; set; } = string.Empty; 

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
