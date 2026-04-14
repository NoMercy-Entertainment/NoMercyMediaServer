namespace NoMercy.Encoder.Output;

using System.Text;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.PostProcess;

public class PlaylistGenerator
{
    public string GenerateMasterPlaylist(OutputPlan plan, string mediaTitle)
    {
        StringBuilder sb = new();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:6");
        sb.AppendLine();

        // Audio groups — keyed by codec for GROUP-ID
        string audioGroupId = "audio_aac";
        if (plan.AudioOutputs.Length > 0)
        {
            string firstCodecName = plan.AudioOutputs[0]
                .EncoderName.Replace("libfdk_", "")
                .Replace("lib", "");
            audioGroupId = $"audio_{firstCodecName}";
        }

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
        {
            if (audio.Action is not (StreamAction.Copy or StreamAction.Transcode))
                continue;

            string codecName = audio.EncoderName.Replace("libfdk_", "").Replace("lib", "");
            Dictionary<string, string> tokens = TemplateResolver.AudioTokens(
                audio.Language ?? "und",
                codecName,
                audio.Channels
            );

            string playlistResolved = TemplateResolver.Resolve(audio.PlaylistNameTemplate, tokens);
            string subDir =
                Path.GetDirectoryName(playlistResolved)?.Replace("\\", "/") ?? playlistResolved;
            string playlistFile = Path.GetFileName(playlistResolved);

            string uri = $"{subDir}/{playlistFile}.m3u8";
            string language = audio.Language ?? "und";
            string displayName = GetAudioDisplayName(language);
            bool isDefault = audio == plan.AudioOutputs[0];

            sb.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"{audioGroupId}\",LANGUAGE=\"{language}\",AUTOSELECT=YES,DEFAULT={YesNo(isDefault)},URI=\"{uri}\",NAME=\"{displayName}\""
            );
        }

        sb.AppendLine();

        // Video variants
        foreach (VideoOutputPlan video in plan.VideoOutputs)
        {
            string codecTag = GetVideoCodecTag(video);
            string audioCodecTag =
                plan.AudioOutputs.Length > 0 ? $",{GetAudioCodecTag(plan.AudioOutputs[0])}" : "";
            int bandwidth =
                video.BitrateKbps > 0 ? video.BitrateKbps * 1000 : EstimateBandwidth(video);

            Dictionary<string, string> tokens = TemplateResolver.VideoTokens(
                video.Width,
                video.Height,
                video.TenBit
            );
            string playlistResolved = TemplateResolver.Resolve(video.PlaylistNameTemplate, tokens);
            string subDir =
                Path.GetDirectoryName(playlistResolved)?.Replace("\\", "/") ?? playlistResolved;
            string playlistFile = Path.GetFileName(playlistResolved);

            string colorRange = video.TenBit ? "HDR" : "SDR";

            string frameRate = video.FrameRate.ToString(
                "F3",
                System.Globalization.CultureInfo.InvariantCulture
            );

            sb.AppendLine(
                $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},AVERAGE-BANDWIDTH={bandwidth},RESOLUTION={video.Width}x{video.Height},FRAME-RATE={frameRate},CODECS=\"{codecTag}{audioCodecTag}\",AUDIO=\"{audioGroupId}\",VIDEO-RANGE={colorRange},COLOUR-SPACE=BT.709,NAME=\"{video.Width}x{video.Height} {colorRange}\""
            );
            sb.AppendLine($"{subDir}/{playlistFile}.m3u8");
        }

        return sb.ToString();
    }

    private static string GetAudioDisplayName(string language)
    {
        return language.ToUpperInvariant() switch
        {
            "ENG" => "English",
            "FRE" or "FRA" => "French",
            "GER" or "DEU" => "German",
            "SPA" => "Spanish",
            "ITA" => "Italian",
            "DUT" or "NLD" => "Dutch",
            "JPN" or "JAP" => "Japanese",
            "KOR" => "Korean",
            "CHI" or "ZHO" => "Chinese",
            "RUS" => "Russian",
            "POR" => "Portuguese",
            "ARA" => "Arabic",
            "HIN" => "Hindi",
            "SWE" => "Swedish",
            "NOR" => "Norwegian",
            "DAN" => "Danish",
            "FIN" => "Finnish",
            "POL" => "Polish",
            "TUR" => "Turkish",
            "UND" => "Unknown",
            _ => language,
        };
    }

    private static string GetVideoCodecTag(VideoOutputPlan video)
    {
        string encoder = video.EncoderName.ToLowerInvariant();

        if (encoder.Contains("264") || encoder.Contains("x264"))
        {
            return video.Level switch
            {
                "4.0" => "avc1.640028",
                "4.1" => "avc1.640029",
                "5.0" => "avc1.640032",
                "5.1" => "avc1.640033",
                _ => "avc1.640028",
            };
        }

        if (encoder.Contains("265") || encoder.Contains("hevc"))
            return video.TenBit ? "hvc1.2.4.L153.B0" : "hvc1.1.6.L93.B0";

        if (encoder.Contains("av1") || encoder.Contains("svtav1") || encoder.Contains("aom"))
            return video.TenBit ? "av01.0.15M.10" : "av01.0.15M.08";

        return "avc1.640028";
    }

    private static string GetAudioCodecTag(AudioOutputPlan audio)
    {
        return audio.EncoderName.ToLowerInvariant() switch
        {
            "aac" or "libfdk_aac" => "mp4a.40.2",
            "ac3" => "ac-3",
            "eac3" => "ec-3",
            "libopus" or "opus" => "opus",
            _ => "mp4a.40.2",
        };
    }

    private static int EstimateBandwidth(VideoOutputPlan video)
    {
        return video.Width switch
        {
            >= 3840 => 15_000_000,
            >= 1920 => 8_000_000,
            >= 1280 => 4_000_000,
            >= 854 => 2_000_000,
            _ => 1_000_000,
        };
    }

    private static string YesNo(bool value) => value ? "YES" : "NO";
}
