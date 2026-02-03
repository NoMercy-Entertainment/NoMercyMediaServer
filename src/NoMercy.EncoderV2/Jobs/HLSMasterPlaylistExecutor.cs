using System.Globalization;
using System.Text;
using NoMercy.Encoder.Core;
using NoMercy.EncoderV2.Core.Dictionaries;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.EncoderV2.Jobs;

/// <summary>
/// Queue-independent executor for HLS master playlist generation
/// Used by both Server (with IShouldQueue) and EncoderNode (with custom queue)
/// Includes proper HDR/SDR support and codec information per HLS spec v6
/// </summary>
public class HlsMasterPlaylistExecutor
{
    /// <summary>
    /// Execute HLS master playlist generation with HDR/SDR detection and codec profiles
    /// </summary>
    public async Task ExecuteAsync(
        string outputDirectory,
        List<QualityPlaylist> videoQualities,
        Dictionary<string, List<AudioVariant>> audioGroups,
        List<SubtitleVariant> subtitleVariants)
    {
        try
        {
            if (videoQualities.Count == 0)
                throw new InvalidOperationException("No video qualities for master playlist generation");

            string masterContent = GenerateMasterPlaylist(videoQualities, audioGroups, subtitleVariants);

            string masterPath = Path.Combine(outputDirectory, "master.m3u8");
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(masterPath, masterContent, Encoding.UTF8);

            Logger.App($"Master playlist generated: {masterPath}");
        }
        catch (Exception ex)
        {
            Logger.App($"HLSMasterPlaylistExecutor failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Generate master.m3u8 with proper HLS v6 attributes including VIDEO-RANGE and CODECS
    /// HDR detection: folder names like video_1920x1080_HDR or video_1920x1080_SDR
    /// </summary>
    private string GenerateMasterPlaylist(
        List<QualityPlaylist> videoQualities,
        Dictionary<string, List<AudioVariant>> audioGroups,
        List<SubtitleVariant> subtitleVariants)
    {
        StringBuilder sb = new();

        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:6");
        sb.AppendLine();

        // Audio media playlists grouped by codec
        foreach ((string codec, List<AudioVariant> tracks) in audioGroups)
        {
            string audioGroupId = $"audio_{codec}";
            foreach (AudioVariant track in tracks)
            {
                string langDisplay = IsoLanguageDictionary.Iso6392ToLanguage.TryGetValue(track.Language, out string? langName)
                    ? ToTitleCase(langName)
                    : ToTitleCase(track.Language);

                string defaultFlag = track.IsDefault ? "YES" : "NO";
                string uri = Path.Combine(track.FolderName, Path.GetFileName(track.PlaylistPath)).Replace("\\", "/");

                sb.AppendLine($"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"{audioGroupId}\",LANGUAGE=\"{track.Language}\",AUTOSELECT=YES,DEFAULT={defaultFlag},URI=\"{uri}\",NAME=\"{langDisplay} {codec.ToUpper()}\"");
            }
        }

        sb.AppendLine();

        // Video stream variants grouped by resolution
        List<IGrouping<string, QualityPlaylist>> resolutionGroups = videoQualities
            .GroupBy(v => v.Resolution)
            .OrderByDescending(g => ParseResolution(g.Key))
            .ToList();

        foreach (IGrouping<string, QualityPlaylist> resolutionGroup in resolutionGroups)
        {
            // Order by SDR first, then HDR
            foreach (QualityPlaylist video in resolutionGroup.OrderByDescending(v => v.IsSdr))
            {
                foreach ((string codec, List<AudioVariant> audioTracks) in audioGroups)
                {
                    string audioCodecStr = MapAudioCodec(codec);
                    string videoCodecStr = video.CodecProfile;
                    string codecs = $"\"{videoCodecStr},{audioCodecStr}\"";
                    string streamName = $"{video.Resolution} {(video.IsSdr ? "SDR" : "HDR")}";
                    string videoRange = video.IsSdr ? "SDR" : "PQ";

                    sb.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={video.Bandwidth},RESOLUTION={video.Resolution},CODECS={codecs},AUDIO=\"audio_{codec}\",VIDEO-RANGE={videoRange},NAME=\"{streamName}\"");
                    sb.AppendLine($"{video.FolderName}/{Path.GetFileName(video.PlaylistPath)}");
                    sb.AppendLine();
                }
            }
        }

        // Subtitle variants
        if (subtitleVariants.Count > 0)
        {
            string closedCaptionGroupId = "cc";
            foreach (SubtitleVariant subtitle in subtitleVariants)
            {
                sb.AppendLine($"#EXT-X-MEDIA:TYPE=CLOSED-CAPTIONS,GROUP-ID=\"{closedCaptionGroupId}\",LANGUAGE=\"{subtitle.Language}\",NAME=\"{ToTitleCase(subtitle.Language)}\",URI=\"{subtitle.PlaylistPath}\"");
            }
            sb.AppendLine();
        }

        sb.AppendLine("#EXT-X-ENDLIST");
        return sb.ToString();
    }

    /// <summary>
    /// Map audio codec name to HLS codec string
    /// </summary>
    private string MapAudioCodec(string codec)
    {
        return codec.ToLowerInvariant() switch
        {
            "aac" => "mp4a.40.2",      // AAC-LC
            "eac3" => "ec-3",          // E-AC-3 (Dolby Digital Plus)
            "ac3" => "ac-3",           // AC-3 (Dolby Digital)
            _ => "mp4a.40.2"           // Default to AAC-LC
        };
    }

    /// <summary>
    /// Parse resolution string (e.g., "1920x1080") to sortable tuple
    /// </summary>
    private (int width, int height) ParseResolution(string resolution)
    {
        if (string.IsNullOrEmpty(resolution)) return (0, 0);
        string[] parts = resolution.Split('x');
        return (int.TryParse(parts[0], out int w) ? w : 0,
                parts.Length > 1 && int.TryParse(parts[1], out int h) ? h : 0);
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }
}

/// <summary>
/// Represents a single video quality variant for HLS master playlist
/// </summary>
public record QualityPlaylist(
    string Resolution,
    int Bitrate,
    string PlaylistPath,
    string FolderName,
    string CodecProfile,
    int Bandwidth,
    bool IsHdr = false)
{
    public bool IsSdr => !IsHdr;
}

/// <summary>
/// Represents an audio variant/track for HLS master playlist
/// </summary>
public record AudioVariant(
    string Language,
    string Codec,
    string PlaylistPath,
    string FolderName,
    bool IsDefault = false);

/// <summary>
/// Represents a subtitle variant for HLS master playlist
/// </summary>
public record SubtitleVariant(
    string Language,
    string Format,
    string PlaylistPath,
    bool IsDefault = false);
