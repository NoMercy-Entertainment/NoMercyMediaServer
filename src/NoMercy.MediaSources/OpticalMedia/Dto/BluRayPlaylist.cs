using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NoMercy.Encoder.Core;

namespace NoMercy.MediaSources.OpticalMedia.Dto;

public partial class BluRayPlaylist
{
    [JsonProperty("complete_name")] public string CompleteName { get; set; } = string.Empty;
    [JsonProperty("playlist_id")] public string PlaylistId { get; set; } = string.Empty;
    [JsonProperty("format")] public string Format { get; set; } = string.Empty;
    [JsonProperty("file_size")] public long FileSize { get; set; }
    [JsonProperty("duration")] public TimeSpan Duration { get; set; }
    [JsonProperty("overall_bit_rate")] public string OverallBitRate { get; set; } = string.Empty;
    [JsonProperty("video_tracks")] public List<VideoTrack> VideoTracks { get; set; } = new();
    [JsonProperty("audio_tracks")] public List<AudioTrack> AudioTracks { get; set; } = new();
    [JsonProperty("subtitle_tracks")] public List<SubtitleTrack> SubtitleTracks { get; set; } = new();
    [JsonProperty("chapters")] public List<Chapter> Chapters { get; set; } = new();

    public static BluRayPlaylist Parse(string input)
    {
        BluRayPlaylist playlist = new();
        string[] lines = input.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        VideoTrack? currentVideo = null;
        AudioTrack? currentAudio = null;
        SubtitleTrack? currentSubtitle = null;
        Chapter? currentChapter = null;
        int index = 0;

        foreach (string line in lines)
        {
            string value = line.Split(':', 2).LastOrDefault()?.Trim() ?? string.Empty;

            if (line.StartsWith("Complete name"))
            {
                playlist.CompleteName = value;
                playlist.PlaylistId = value.Split('\\').LastOrDefault()
                    ?.Split('.').FirstOrDefault() ?? string.Empty;
            }
            else if (line.StartsWith("Format "))
            {
                playlist.Format = value;
            }
            else if (line.StartsWith("File size"))
            {
                playlist.FileSize = ParseInt(value);
            }
            else if (line.StartsWith("Duration"))
            {
                playlist.Duration = ParseDuration(value);
            }
            else if (line.StartsWith("Overall bit rate"))
            {
                playlist.OverallBitRate = value;
            }
            else if (line.StartsWith("Video"))
            {
                currentVideo = new()
                {
                    StreamIndex = index++
                };
            }
            else if (line.StartsWith("Audio"))
            {
                currentAudio = new()
                {
                    StreamIndex = index++
                };
            }
            else if (line.StartsWith("Text"))
            {
                currentSubtitle = new()
                {
                    StreamIndex = index++
                };
            }
            else if (line.StartsWith("Menu"))
            {
                currentChapter = new();
            }

            if (currentVideo != null)
            {
                if (line.StartsWith("ID"))
                {
                    currentVideo.Id = ParseInt(value);
                }
                else if (line.StartsWith("Format") && currentVideo.Format == null)
                {
                    currentVideo.Format = value;
                }
                else if (line.StartsWith("Format/Info"))
                {
                    currentVideo.FormatInfo = value;
                }
                else if (line.StartsWith("Width"))
                {
                    currentVideo.Width = ParseInt(value);
                }
                else if (line.StartsWith("Height"))
                {
                    currentVideo.Height = ParseInt(value);
                }
                else if (line.StartsWith("Display aspect ratio"))
                {
                    currentVideo.DisplayAspectRatio = value;
                }
                else if (line.StartsWith("Frame rate"))
                {
                    currentVideo.FrameRate =
                        double.Parse(Regex.Match(line, "[0-9.]+").Value, CultureInfo.InvariantCulture);
                    playlist.VideoTracks.Add(currentVideo);
                    currentVideo = null;
                }
            }

            if (currentAudio != null)
            {
                if (line.StartsWith("ID"))
                {
                    currentAudio.Id = ParseInt(value);
                }
                else if (line.StartsWith("Format") && currentAudio.Format == null)
                {
                    currentAudio.Format = value;
                }
                else if (line.StartsWith("Format/Info"))
                {
                    currentAudio.FormatInfo = value;
                }
                else if (line.StartsWith("Channel(s)"))
                {
                    currentAudio.Channels = ParseInt(value);
                }
                else if (line.StartsWith("Sampling rate"))
                {
                    currentAudio.SamplingRate = ParseInt(value);
                }
                else if (line.StartsWith("Compression mode"))
                {
                    currentAudio.CompressionMode = value;
                }
                else if (line.StartsWith("Duration"))
                {
                    currentAudio.Duration = ParseDuration(value);
                }
                else if (line.StartsWith("Language"))
                {
                    currentAudio.Language = value;
                    currentAudio.Lang = IsoLanguageMapper.GetIsoCode(value);
                    playlist.AudioTracks.Add(currentAudio);
                    currentAudio = null;
                }
            }

            if (currentSubtitle != null)
            {
                if (line.StartsWith("ID"))
                {
                    currentSubtitle.Id = ParseInt(value);
                }
                else if (line.StartsWith("Format"))
                {
                    currentSubtitle.Format = value;
                }
                else if (line.StartsWith("Duration"))
                {
                    currentSubtitle.Duration = ParseDuration(value);
                }
                else if (line.StartsWith("Language"))
                {
                    currentSubtitle.Language = value;
                    playlist.SubtitleTracks.Add(currentSubtitle);
                    currentSubtitle = null;
                }
            }

            if (currentChapter == null) continue;

            Chapter? chapter = ParseChapter(line);
            if (chapter != null) playlist.Chapters.Add(chapter);
        }

        return playlist;
    }

    private static TimeSpan ParseDuration(string input)
    {
        Match match = Regex.Match(input, @"(?:(\d+)\s*hour[s]?)?\s*(?:(\d+)\s*min[s]?)?\s*(?:(\d+)\s*s)");
        int hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        int minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        int seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        return new(hours, minutes, seconds);
    }

    private static int ParseInt(string input)
    {
        return int.Parse(ValueRegex().Match(input).Value.Replace(" ", ""));
    }

    public static Chapter? ParseChapter(string line)
    {
        Match match = Regex.Match(line, @"(\d+:\d+:\d+.\d+)\s*:\s(.*)");
        if (match.Success)
            return new()
            {
                Timestamp = TimeSpan.Parse(match.Groups[1].Value),
                Title = match.Groups[2].Value
            };

        return null;
    }

    [GeneratedRegex(@"[\d\s]+")]
    private static partial Regex ValueRegex();
}

public class VideoTrack
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("stream_index")] public int StreamIndex { get; set; }
    [JsonProperty("format")] public string? Format { get; set; }
    [JsonProperty("format_info")] public string FormatInfo { get; set; } = string.Empty;
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("display_aspect_ratio")] public string DisplayAspectRatio { get; set; } = string.Empty;
    [JsonProperty("frame_rate")] public double FrameRate { get; set; }
}

public class AudioTrack
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("stream_index")] public int StreamIndex { get; set; }
    [JsonProperty("format")] public string? Format { get; set; }
    [JsonProperty("format_info")] public string FormatInfo { get; set; } = string.Empty;
    [JsonProperty("commercial_name")] public string? CommercialName { get; set; }
    [JsonProperty("duration")] public TimeSpan Duration { get; set; }
    [JsonProperty("channels")] public int? Channels { get; set; }
    [JsonProperty("sampling_rate")] public int SamplingRate { get; set; } // Hz
    [JsonProperty("compression_mode")] public string? CompressionMode { get; set; }
    [JsonProperty("language")] public string Language { get; set; } = string.Empty;
    [JsonProperty("lang")] public string? Lang { get; set; }
}

public class SubtitleTrack
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("stream_index")] public int StreamIndex { get; set; }
    [JsonProperty("format")] public string Format { get; set; } = string.Empty;
    [JsonProperty("duration")] public TimeSpan Duration { get; set; }
    [JsonProperty("language")] public string Language { get; set; } = string.Empty;
}

public class Chapter
{
    [JsonProperty("timestamp")] public TimeSpan Timestamp { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
}