namespace NoMercy.Encoder.V3.Analysis;

using Newtonsoft.Json.Linq;
using NoMercy.Encoder.V3.Infrastructure;

public class MediaAnalyzer(IProcessRunner processRunner) : IMediaAnalyzer
{
    private static readonly string[] FfprobeArgs =
    [
        "-v",
        "quiet",
        "-print_format",
        "json",
        "-show_format",
        "-show_streams",
        "-show_chapters",
    ];

    public async Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken ct = default)
    {
        string[] arguments = [.. FfprobeArgs, filePath];
        ProcessResult result = await processRunner.RunAsync("ffprobe", arguments, null, ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"ffprobe failed for '{filePath}': {result.StdErr}"
            );

        return ParseFfprobeJson(result.StdOut, filePath);
    }

    internal static MediaInfo ParseFfprobeJson(string json, string filePath)
    {
        JObject root = JObject.Parse(json);
        JArray streams = root["streams"] as JArray ?? [];
        JArray chapters = root["chapters"] as JArray ?? [];
        JObject format = root["format"] as JObject ?? new JObject();

        List<VideoStreamInfo> videoStreams = [];
        List<AudioStreamInfo> audioStreams = [];
        List<SubtitleStreamInfo> subtitleStreams = [];

        foreach (JToken stream in streams)
        {
            string codecType = stream.Value<string>("codec_type") ?? "";
            switch (codecType)
            {
                case "video":
                    videoStreams.Add(ParseVideoStream(stream));
                    break;
                case "audio":
                    audioStreams.Add(ParseAudioStream(stream));
                    break;
                case "subtitle":
                    subtitleStreams.Add(ParseSubtitleStream(stream));
                    break;
            }
        }

        List<ChapterInfo> chapterList = [];
        foreach (JToken chapter in chapters)
        {
            chapterList.Add(
                new ChapterInfo(
                    Start: TimeSpan.FromSeconds(chapter.Value<double>("start_time")),
                    End: TimeSpan.FromSeconds(chapter.Value<double>("end_time")),
                    Title: chapter["tags"]?.Value<string>("title")
                )
            );
        }

        double durationSeconds = format.Value<double>("duration");
        long bitRate = ParseLong(format, "bit_rate");
        long fileSize = ParseLong(format, "size");
        string formatName = format.Value<string>("format_name") ?? "unknown";

        return new MediaInfo(
            FilePath: filePath,
            Format: formatName,
            Duration: TimeSpan.FromSeconds(durationSeconds),
            OverallBitRateKbps: bitRate / 1000,
            FileSizeBytes: fileSize,
            VideoStreams: videoStreams,
            AudioStreams: audioStreams,
            SubtitleStreams: subtitleStreams,
            Chapters: chapterList
        );
    }

    private static VideoStreamInfo ParseVideoStream(JToken stream)
    {
        string pixFmt = stream.Value<string>("pix_fmt") ?? "yuv420p";
        int bitDepth = ParseBitDepth(stream, pixFmt);
        double frameRate = ParseFrameRate(stream.Value<string>("r_frame_rate") ?? "24/1");
        double? avgFrameRate = ParseNullableFrameRate(stream.Value<string>("avg_frame_rate"));
        double? realFrameRate = ParseNullableFrameRate(stream.Value<string>("r_frame_rate"));

        return new VideoStreamInfo(
            Index: stream.Value<int>("index"),
            Codec: stream.Value<string>("codec_name") ?? "unknown",
            Width: stream.Value<int>("width"),
            Height: stream.Value<int>("height"),
            FrameRate: frameRate,
            BitDepth: bitDepth,
            PixelFormat: pixFmt,
            ColorPrimaries: stream.Value<string>("color_primaries"),
            ColorTransfer: stream.Value<string>("color_transfer"),
            ColorSpace: stream.Value<string>("color_space"),
            IsDefault: stream["disposition"]?.Value<int>("default") == 1,
            BitRateKbps: ParseLong(stream, "bit_rate") / 1000,
            AverageFrameRate: avgFrameRate,
            RealFrameRate: realFrameRate
        );
    }

    private static AudioStreamInfo ParseAudioStream(JToken stream)
    {
        return new AudioStreamInfo(
            Index: stream.Value<int>("index"),
            Codec: stream.Value<string>("codec_name") ?? "unknown",
            Channels: stream.Value<int>("channels"),
            SampleRate: stream.Value<int>("sample_rate"),
            BitRateKbps: ParseLong(stream, "bit_rate") / 1000,
            Language: stream["tags"]?.Value<string>("language"),
            IsDefault: stream["disposition"]?.Value<int>("default") == 1,
            IsForced: stream["disposition"]?.Value<int>("forced") == 1
        );
    }

    private static SubtitleStreamInfo ParseSubtitleStream(JToken stream)
    {
        return new SubtitleStreamInfo(
            Index: stream.Value<int>("index"),
            Codec: stream.Value<string>("codec_name") ?? "unknown",
            Language: stream["tags"]?.Value<string>("language"),
            IsDefault: stream["disposition"]?.Value<int>("default") == 1,
            IsForced: stream["disposition"]?.Value<int>("forced") == 1
        );
    }

    private static int ParseBitDepth(JToken stream, string pixFmt)
    {
        string? bitsRaw = stream.Value<string>("bits_per_raw_sample");
        if (bitsRaw is not null && int.TryParse(bitsRaw, out int bits))
            return bits;
        return pixFmt.Contains("10") ? 10 : 8;
    }

    private static double ParseFrameRate(string frac)
    {
        string[] parts = frac.Split('/');
        if (
            parts.Length == 2
            && double.TryParse(parts[0], out double num)
            && double.TryParse(parts[1], out double den)
            && den > 0
        )
            return num / den;
        return 24.0;
    }

    private static double? ParseNullableFrameRate(string? frac)
    {
        if (frac is null)
            return null;
        string[] parts = frac.Split('/');
        if (
            parts.Length == 2
            && double.TryParse(parts[0], out double num)
            && double.TryParse(parts[1], out double den)
            && den > 0
        )
            return num / den;
        return null;
    }

    private static long ParseLong(JToken token, string key)
    {
        string? val = token.Value<string>(key);
        return val is not null && long.TryParse(val, out long result) ? result : 0;
    }
}
