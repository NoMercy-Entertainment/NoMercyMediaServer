using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;
public class TrackRecording
{
    [JsonProperty("aliases")] public Alias[] Aliases { get; set; } = [];
    [JsonProperty("artist-credit")] public RecordingArtistCredit[] ArtistCredit { get; set; } = [];
    [JsonProperty("disambiguation")] public string Disambiguation { get; set; } = string.Empty;

    // ReSharper disable once InconsistentNaming
    [JsonProperty("first-release-date")] private string? _firstReleaseDate { get; set; }

    public DateTime? FirstReleaseDate
    {
        get => !string.IsNullOrWhiteSpace(_firstReleaseDate) && !string.IsNullOrEmpty(_firstReleaseDate) && _firstReleaseDate.TryParseToDateTime(out DateTime dt) ? dt : null;
        set => _firstReleaseDate = value.ToString() ?? string.Empty;
    }

    [JsonProperty("genres")] public MusicBrainzGenreDetails[] Genres { get; set; } = [];
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("isrcs")] public string[] Isrcs { get; set; } = [];
    [JsonProperty("length")] public int? Length { get; set; }
    [JsonProperty("relations")] public RecordingRelation[] Relations { get; set; } = [];
    [JsonProperty("tags")] public MusicBrainzTag[] Tags { get; set; } = [];
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("video")] public bool Video { get; set; }
}