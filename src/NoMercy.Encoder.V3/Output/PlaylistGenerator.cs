namespace NoMercy.Encoder.V3.Output;

using System.Text;
using NoMercy.Encoder.V3.Pipeline;

public class PlaylistGenerator
{
    public string GenerateMasterPlaylist(OutputPlan plan)
    {
        StringBuilder sb = new();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:7");
        sb.AppendLine();

        // Audio groups
        foreach (AudioOutputPlan audio in plan.AudioOutputs)
        {
            if (audio.Action is not (StreamAction.Copy or StreamAction.Transcode))
                continue;

            string subDir = $"audio_{audio.Language ?? "und"}_{audio.Channels}ch";
            string uri = $"{subDir}/{subDir}.m3u8";
            string language = audio.Language ?? "und";
            bool isDefault = audio == plan.AudioOutputs[0];

            sb.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio\",NAME=\"{language}\",LANGUAGE=\"{language}\",DEFAULT={YesNo(isDefault)},AUTOSELECT=YES,URI=\"{uri}\""
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
            string subDir = $"video_{video.Width}x{video.Height}";

            sb.AppendLine(
                $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={video.Width}x{video.Height},CODECS=\"{codecTag}{audioCodecTag}\",AUDIO=\"audio\""
            );
            sb.AppendLine($"{subDir}/{subDir}.m3u8");
        }

        return sb.ToString();
    }

    private static string GetVideoCodecTag(VideoOutputPlan video)
    {
        string encoder = video.EncoderName.ToLowerInvariant();

        // H.264 encoders
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

        // HEVC encoders
        if (encoder.Contains("265") || encoder.Contains("hevc"))
            return video.TenBit ? "hvc1.2.4.L153.B0" : "hvc1.1.6.L93.B0";

        // AV1 encoders
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
