using System.Text.Json;
using System.Text.Json.Serialization;
using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Services;

/// <summary>
/// Default implementation of IMediaAnalyzer using FFprobe
/// </summary>
public sealed class MediaAnalyzer : IMediaAnalyzer
{
    private readonly IFFmpegExecutor _executor;
    private const int TimeoutMs = 30000;
    private const int MaxRetries = 3;

    public MediaAnalyzer(IFFmpegExecutor executor)
    {
        _executor = executor;
    }

    public async Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Media file not found", filePath);
        }

        string arguments = $"-v quiet -print_format json -show_format -show_streams -show_chapters \"{filePath}\"";

        FFmpegResult? result = null;
        Exception? lastException = null;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeoutMs);

                result = await ExecuteFFprobeAsync(arguments, cts.Token);

                if (result.Success)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException($"FFprobe timed out after {TimeoutMs}ms (attempt {attempt + 1}/{MaxRetries})");
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            if (attempt < MaxRetries - 1)
            {
                await Task.Delay(100 * (attempt + 1), cancellationToken);
            }
        }

        if (result == null || !result.Success)
        {
            throw lastException ?? new InvalidOperationException($"FFprobe failed: {result?.StandardError}");
        }

        return ParseProbeOutput(filePath, result.StandardOutput);
    }

    public async Task<TimeSpan> GetDurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        string arguments = $"-v quiet -print_format json -show_entries format=duration \"{filePath}\"";

        FFmpegResult result = await ExecuteFFprobeAsync(arguments, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"FFprobe failed: {result.StandardError}");
        }

        using JsonDocument doc = JsonDocument.Parse(result.StandardOutput);
        if (doc.RootElement.TryGetProperty("format", out JsonElement format) &&
            format.TryGetProperty("duration", out JsonElement duration))
        {
            if (double.TryParse(duration.GetString(), out double seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        throw new InvalidOperationException("Could not parse duration from FFprobe output");
    }

    public async Task<bool> IsValidMediaFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            string arguments = $"-v quiet -print_format json -show_entries format=format_name \"{filePath}\"";
            FFmpegResult result = await ExecuteFFprobeAsync(arguments, cancellationToken);
            return result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    private async Task<FFmpegResult> ExecuteFFprobeAsync(string arguments, CancellationToken cancellationToken)
    {
        System.Diagnostics.ProcessStartInfo startInfo = new()
        {
            FileName = _executor.FFprobePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using System.Diagnostics.Process process = new() { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new FFmpegResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            StandardOutput = output,
            StandardError = error
        };
    }

    private static MediaInfo ParseProbeOutput(string filePath, string json)
    {
        FFprobeOutput output = JsonSerializer.Deserialize<FFprobeOutput>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse FFprobe output");

        List<VideoStreamInfo> videoStreams = [];
        List<AudioStreamInfo> audioStreams = [];
        List<SubtitleStreamInfo> subtitleStreams = [];
        List<ChapterInfo> chapters = [];

        foreach (FFprobeStream stream in output.Streams ?? [])
        {
            switch (stream.CodecType)
            {
                case "video" when stream.Disposition?.AttachedPic != 1:
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

        foreach (FFprobeChapter chapter in output.Chapters ?? [])
        {
            chapters.Add(new ChapterInfo
            {
                Index = chapter.Id,
                Title = chapter.Tags?.Title,
                StartTime = TimeSpan.FromSeconds(ParseDouble(chapter.StartTime)),
                EndTime = TimeSpan.FromSeconds(ParseDouble(chapter.EndTime))
            });
        }

        return new MediaInfo
        {
            FilePath = filePath,
            Duration = TimeSpan.FromSeconds(ParseDouble(output.Format?.Duration)),
            FileSize = ParseLong(output.Format?.Size),
            Format = output.Format?.FormatName,
            Bitrate = ParseLong(output.Format?.BitRate),
            VideoStreams = videoStreams,
            AudioStreams = audioStreams,
            SubtitleStreams = subtitleStreams,
            Chapters = chapters
        };
    }

    private static VideoStreamInfo ParseVideoStream(FFprobeStream stream)
    {
        bool isHdr = IsHdrStream(stream);

        return new VideoStreamInfo
        {
            Index = stream.Index,
            Codec = stream.CodecName,
            Profile = stream.Profile,
            Width = stream.Width ?? 0,
            Height = stream.Height ?? 0,
            FrameRate = ParseFrameRate(stream.RFrameRate),
            Bitrate = ParseLong(stream.BitRate),
            PixelFormat = stream.PixFmt,
            ColorSpace = stream.ColorSpace,
            ColorTransfer = stream.ColorTransfer,
            ColorPrimaries = stream.ColorPrimaries,
            IsHdr = isHdr,
            IsInterlaced = stream.FieldOrder != null && stream.FieldOrder != "progressive",
            Language = stream.Tags?.Language,
            IsDefault = stream.Disposition?.Default == 1,
            Duration = TimeSpan.FromSeconds(ParseDouble(stream.Duration))
        };
    }

    private static AudioStreamInfo ParseAudioStream(FFprobeStream stream)
    {
        return new AudioStreamInfo
        {
            Index = stream.Index,
            Codec = stream.CodecName,
            Profile = stream.Profile,
            Channels = stream.Channels ?? 0,
            ChannelLayout = stream.ChannelLayout,
            SampleRate = ParseInt(stream.SampleRate),
            Bitrate = ParseLong(stream.BitRate),
            Language = stream.Tags?.Language,
            Title = stream.Tags?.Title,
            IsDefault = stream.Disposition?.Default == 1,
            IsForced = stream.Disposition?.Forced == 1,
            Duration = TimeSpan.FromSeconds(ParseDouble(stream.Duration))
        };
    }

    private static SubtitleStreamInfo ParseSubtitleStream(FFprobeStream stream)
    {
        return new SubtitleStreamInfo
        {
            Index = stream.Index,
            Codec = stream.CodecName,
            Language = stream.Tags?.Language,
            Title = stream.Tags?.Title,
            IsDefault = stream.Disposition?.Default == 1,
            IsForced = stream.Disposition?.Forced == 1,
            IsHearingImpaired = stream.Disposition?.HearingImpaired == 1
        };
    }

    private static bool IsHdrStream(FFprobeStream stream)
    {
        // Check for HDR indicators
        string[] hdrTransfers = ["smpte2084", "arib-std-b67", "bt2020-10", "bt2020-12"];
        string[] hdrColorSpaces = ["bt2020nc", "bt2020c"];

        if (stream.ColorTransfer != null && hdrTransfers.Contains(stream.ColorTransfer.ToLowerInvariant()))
        {
            return true;
        }

        if (stream.ColorSpace != null && hdrColorSpaces.Contains(stream.ColorSpace.ToLowerInvariant()))
        {
            return true;
        }

        // Check pixel format for 10-bit or higher
        if (stream.PixFmt != null && (stream.PixFmt.Contains("10le") || stream.PixFmt.Contains("10be") ||
            stream.PixFmt.Contains("12le") || stream.PixFmt.Contains("12be")))
        {
            return true;
        }

        return false;
    }

    private static double ParseFrameRate(string? frameRate)
    {
        if (string.IsNullOrEmpty(frameRate)) return 0;

        string[] parts = frameRate.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out double num) &&
            double.TryParse(parts[1], out double den) &&
            den > 0)
        {
            return num / den;
        }

        return double.TryParse(frameRate, out double result) ? result : 0;
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(value, out double result) ? result : 0;
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, out int result) ? result : 0;
    }

    private static long ParseLong(string? value)
    {
        return long.TryParse(value, out long result) ? result : 0;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    #region FFprobe DTOs

    private sealed class FFprobeOutput
    {
        [JsonPropertyName("streams")]
        public List<FFprobeStream>? Streams { get; set; }

        [JsonPropertyName("format")]
        public FFprobeFormat? Format { get; set; }

        [JsonPropertyName("chapters")]
        public List<FFprobeChapter>? Chapters { get; set; }
    }

    private sealed class FFprobeStream
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("codec_name")]
        public string? CodecName { get; set; }

        [JsonPropertyName("codec_type")]
        public string? CodecType { get; set; }

        [JsonPropertyName("profile")]
        public string? Profile { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("r_frame_rate")]
        public string? RFrameRate { get; set; }

        [JsonPropertyName("bit_rate")]
        public string? BitRate { get; set; }

        [JsonPropertyName("pix_fmt")]
        public string? PixFmt { get; set; }

        [JsonPropertyName("color_space")]
        public string? ColorSpace { get; set; }

        [JsonPropertyName("color_transfer")]
        public string? ColorTransfer { get; set; }

        [JsonPropertyName("color_primaries")]
        public string? ColorPrimaries { get; set; }

        [JsonPropertyName("field_order")]
        public string? FieldOrder { get; set; }

        [JsonPropertyName("channels")]
        public int? Channels { get; set; }

        [JsonPropertyName("channel_layout")]
        public string? ChannelLayout { get; set; }

        [JsonPropertyName("sample_rate")]
        public string? SampleRate { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("tags")]
        public FFprobeTags? Tags { get; set; }

        [JsonPropertyName("disposition")]
        public FFprobeDisposition? Disposition { get; set; }
    }

    private sealed class FFprobeFormat
    {
        [JsonPropertyName("format_name")]
        public string? FormatName { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("bit_rate")]
        public string? BitRate { get; set; }
    }

    private sealed class FFprobeChapter
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("tags")]
        public FFprobeTags? Tags { get; set; }
    }

    private sealed class FFprobeTags
    {
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    private sealed class FFprobeDisposition
    {
        [JsonPropertyName("default")]
        public int? Default { get; set; }

        [JsonPropertyName("forced")]
        public int? Forced { get; set; }

        [JsonPropertyName("hearing_impaired")]
        public int? HearingImpaired { get; set; }

        [JsonPropertyName("attached_pic")]
        public int? AttachedPic { get; set; }
    }

    #endregion
}
