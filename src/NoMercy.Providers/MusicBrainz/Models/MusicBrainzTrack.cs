using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzTrack
{
    private int _number;
    [JsonProperty("artist-credit")] public ReleaseArtistCredit[] ArtistCredit { get; set; } = [];
    [JsonProperty("id")] public Guid Id { get; set; }
    
    /** Track length in milliseconds */
    [JsonProperty("length")] public int? Length { get; set; }
    public double Duration => (Length ?? 0) / 1000.0;

    [JsonProperty("number")]
    public int Number
    {
        get => _number;
        set
        {
            try
            {
                _number = Convert.ToInt32(value);
            }
            catch (Exception)
            {
                _number = value.ToString().Replace("A", "").Split("-").LastOrDefault()?.ToInt() ?? 0;
            }
        }
    }

    [JsonProperty("position")] public int Position { get; set; }
    [JsonProperty("recording")] public TrackRecording Recording { get; set; } = new();
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("genres")] public MusicBrainzGenreDetails[]? Genres { get; set; }
}