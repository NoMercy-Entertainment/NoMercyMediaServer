using System.Globalization;
using System.Text.RegularExpressions;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Lrclib.Client;
using NoMercy.Providers.MusixMatch.Client;
using NoMercy.Providers.MusixMatch.Models;

namespace NoMercy.Providers.NoMercy.Client;

public static partial class NoMercyLyricsClient
{
    public static async Task<dynamic?> SearchLyrics(Track track)
    {
        MusixmatchClient musixmatchClient = new();
        LrclibClient lrclibClient = new();
        dynamic? lyric = null;
        int recursiveCount = 0;
        string artistNames = string.Join(",", track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name));
        string duration = track.Duration.ToSeconds().ToString(CultureInfo.InvariantCulture);
        string albumName = track.AlbumTrack.FirstOrDefault()?.Album.Name ?? string.Empty;
        while (true)
        {
            MusixMatchSubtitleGet? lyrics = null;
            switch (recursiveCount)
            {
                case 0:
                case 4:
                    if (recursiveCount == 4)
                    {
                        lyric = await lrclibClient.SongSearch(
                            artists: track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                            trackName: track.Name,
                            albumName: track.AlbumTrack.FirstOrDefault()?.Album.Name,
                            duration: track.Duration?.ToSeconds()
                        );
                        lyric ??= ToFormatLyrics(lyrics);
                        break;
                    }
                    lyrics = await musixmatchClient.SongSearch(new() { Album = albumName, Artist = artistNames, Title = track.Name, Duration = duration, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
                case 1:
                case 5:
                    if (recursiveCount == 5)
                    {
                        lyric = await lrclibClient.SongSearch(
                            artists: track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                            trackName: track.Name,
                            duration: track.Duration?.ToSeconds()
                        );
                        lyric ??= ToFormatLyrics(lyrics);
                        break;
                    }
                    lyrics = await musixmatchClient.SongSearch(new() { Artist = artistNames, Title = track.Name, Duration = duration, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
                case 2:
                case 6:
                    if (recursiveCount == 6)
                    {
                        lyric = await lrclibClient.SongSearch(
                            artists: track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                            trackName: track.Name
                        );
                        lyric ??= ToFormatLyrics(lyrics);
                        break;
                    }
                    lyrics = await musixmatchClient.SongSearch(new() { Artist = artistNames, Title = track.Name, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
                case 3:
                case 7:
                    if (recursiveCount == 7)
                    {
                        lyric = ToFormatLyrics(lyrics);
                        break;
                    }
                    lyrics = await musixmatchClient.SongSearch(new() { Title = track.Name, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
            }
            if (lyric is not null || recursiveCount >= 7) break;
            recursiveCount += 1;
        }
        musixmatchClient.Dispose();
        lrclibClient.Dispose();
        return lyric;
    }

    private static dynamic? ToFormatLyrics(MusixMatchSubtitleGet? lyrics)
    {
        string text = FormatLyricsRegex().Replace(input: lyrics?.Message?.Body?.MacroCalls?.TrackLyricsGet?.Message?.Body?.Lyrics?.LyricsBody ?? string.Empty, replacement: "");
        if (string.IsNullOrEmpty(text))
            return null;
        
        return new[]{
            new MusixMatchFormattedLyric
            {
                Text = text,
                Time = new()
                {
                    Total = 0.0,
                    Minutes = 0,
                    Seconds = 0,
                    Hundredths = 0
                }
            }
        };
    }

    [GeneratedRegex("^\"|\"$")]
    private static partial Regex FormatLyricsRegex();
}