using System.Globalization;
using System.Text.RegularExpressions;
using NoMercy.Providers.Lrclib.Models;
using NoMercy.Providers.MusixMatch.Models;

namespace NoMercy.Providers.Lrclib.Client;

public partial class LrclibClient : LrclibBaseClient
{
    public async Task<MusixMatchFormattedLyric[]?> SongSearch(
        string[] artists, 
        string trackName, 
        string? albumName = null, 
        double? duration = null, 
        bool priority = false
    )
    {
        Dictionary<string, string> additionalArguments = new()
        {
            { "artist_name", string.Join(",", artists) },
            { "track_name", trackName }
        };
        if (albumName != null)
            additionalArguments.Add("album_name", albumName);
        if (duration.HasValue)
            additionalArguments.Add("duration", duration.Value.ToString(CultureInfo.InvariantCulture));
        
        LrclibSongResult? result = await Get<LrclibSongResult>("",additionalArguments, priority);
        if (
                !string.IsNullOrEmpty(result?.Message) || 
                result?.StatusCode != 200 || 
                result.Name == "TrackNotFound"
            )
            return null;
        return ConvertToMusixmatchLyrics(!string.IsNullOrEmpty(result.SyncedLyrics) ? result.SyncedLyrics : result.PlainLyrics);
    }
    
    private static MusixMatchFormattedLyric[]? ConvertToMusixmatchLyrics(string? lyrics)
    {
        if (string.IsNullOrEmpty(lyrics)) return null;
        string[] lines = lyrics.Split(['\r', '\n'], StringSplitOptions.None);
        
        List<MusixMatchFormattedLyric> lyricLines = [];
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;
            MusixMatchFormattedLyric? lyricLine = MakeLyricLine(trimmedLine);
            if (lyricLine != null)
                lyricLines.Add(lyricLine);
        }
        return lyricLines.Count == 0 ? null : lyricLines.ToArray();
    }

    private static MusixMatchFormattedLyric? MakeLyricLine(string trimmedLine)
    {
        if (string.IsNullOrEmpty(trimmedLine)) return null;
        
        Match match = TimeStamped().Match(trimmedLine);
        if (!match.Success)
            return new()
            {
                Text = trimmedLine,
                Time = new()
                {
                    Total = 0,
                    Minutes = 0,
                    Seconds = 0,
                    Hundredths = 0
                }
            };
        
        int minutes = int.Parse(match.Groups[1].Value);
        int seconds = int.Parse(match.Groups[2].Value);
        int hundredths = int.Parse(match.Groups[3].Value);
        string text = match.Groups[4].Value.Trim();
        double total = (minutes * 60) + seconds + (hundredths / 100.0);
            
        return new()
        {
            Text = text,
            Time = new()
            {
                Total = total,
                Minutes = minutes,
                Seconds = seconds,
                Hundredths = hundredths
            }
        };
    }

    [GeneratedRegex(@"\[(\d{2}):(\d{2})\.(\d{2})\](.*)$")]
    private static partial Regex TimeStamped();
}
    
