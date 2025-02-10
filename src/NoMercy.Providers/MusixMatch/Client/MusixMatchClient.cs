using NoMercy.Providers.MusixMatch.Models;
using static System.String;

namespace NoMercy.Providers.MusixMatch.Client;

public class MusixmatchClient : MusixMatchBaseClient
{
    public Task<MusixMatchSubtitleGet?> SongSearch(MusixMatchTrackSearchParameters musixMatchTrackParameters,
        bool priority = false)
    {
        Dictionary<string, string?> additionalArguments = new()
        {
            ["q_artist"] = musixMatchTrackParameters.Artist,
            ["q_artists"] = Join(",", musixMatchTrackParameters.Artists ?? []),
            ["q_track"] = musixMatchTrackParameters.Title,
            // ["q_album"] = trackParameters.Album,
            ["q_duration"] = musixMatchTrackParameters.Duration ?? Empty
        };

        return Get<MusixMatchSubtitleGet>("macro.subtitles.get", additionalArguments, priority);
    }
}