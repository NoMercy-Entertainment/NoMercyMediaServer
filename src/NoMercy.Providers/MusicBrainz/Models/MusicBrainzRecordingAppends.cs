using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzRecordingAppends : MusicBrainzRecording
{
    // ReSharper disable once InconsistentNaming
    [JsonProperty("first-release-date")] private string? _firstReleaseDate { get; set; }

    public DateTime? FirstReleaseDate
    {
        get => !string.IsNullOrWhiteSpace(_firstReleaseDate) && !string.IsNullOrEmpty(_firstReleaseDate) &&
               _firstReleaseDate.TryParseToDateTime(out DateTime dt)
            ? dt
            : null;
        set => _firstReleaseDate = value.ToString();
    }

    [JsonProperty("media")] public MusicBrainzMedia[] Media { get; set; } = [];
    [JsonProperty("tags")] public MusicBrainzTag[] Tags { get; set; } = [];
    [JsonProperty("releases")] public MusicBrainzRelease[] Releases { get; set; } = [];
    [JsonProperty("artist-credit")] public MusicBrainzArtistCredit[] ArtistCredit { get; set; } = [];
}

public class MusicBrainzSearchResponse
{
    [JsonProperty("created")] public DateTime Created { get; set; }

    [JsonProperty("count")] public int Count { get; set; }

    [JsonProperty("offset")] public int Offset { get; set; }

    [JsonProperty("recordings")] public List<MusicBrainzSearchRecording> Recordings { get; set; } = [];
}

public class MusicBrainzSearchRecording : MusicBrainzRecordingAppends
{
    [JsonProperty("score")] public int Score { get; set; }
}