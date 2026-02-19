namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackSearchParameters
{
    public static Dictionary<MusixMatchSortStrategy, KeyValuePair<string, string>> StrategyDecryptions = new()
    {
        [MusixMatchSortStrategy.TrackRatingAsc] = new("s_track_rating", "asc"),
        [MusixMatchSortStrategy.TrackRatingDesc] = new("s_track_rating", "desc"),
        [MusixMatchSortStrategy.ArtistRatingAsc] = new("s_artist_rating", "asc"),
        [MusixMatchSortStrategy.ArtistRatingDesc] = new("s_artist_rating", "desc"),
        [MusixMatchSortStrategy.ReleaseDateAsc] = new("s_track_release_date", "asc"),
        [MusixMatchSortStrategy.ReleaseDateDesc] = new("s_track_release_date", "desc")
    };

    public string? Query { get; set; }
    public string? LyricsQuery { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string[]? Artists { get; set; }
    public string? Album { get; init; }
    public string? Duration { get; set; }
    public string? Language { get; set; }
    public bool? HasLyrics { get; set; }
    public bool? HasSubtitles { get; set; }
    public bool? HasRichSync { get; set; }
    public MusixMatchSortStrategy? Sort { get; init; } = MusixMatchSortStrategy.TrackRatingDesc;


    public enum MusixMatchSortStrategy
    {
        TrackRatingAsc,
        TrackRatingDesc,
        ArtistRatingAsc,
        ArtistRatingDesc,
        ReleaseDateAsc,
        ReleaseDateDesc
    }
}