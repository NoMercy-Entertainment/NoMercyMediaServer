using System.Diagnostics;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem;

public static class FfProbe
{
    private const int ExecutionTimeoutMs = 30000;
    private const int MaxRetries = 3;

    public static async Task<FfProbeData> CreateAsync(string file, CancellationToken ct = default)
    {
        try
        {
            string json = await RunFfprobeWithRetry(file, ct);
            if (string.IsNullOrEmpty(json))
                return new() { ErrorData = ["ffprobe returned empty output"] };

            FfProbeRawResult? raw = json.FromJson<FfProbeRawResult>();
            if (raw is null)
                return new() { ErrorData = ["Failed to parse ffprobe output"] };

            return BuildFfProbeData(file, raw);
        }
        catch (Exception ex)
        {
            Logger.App($"FfProbe failed for: {file}: {ex.Message}", LogEventLevel.Warning);
            return new() { ErrorData = [ex.Message] };
        }
    }

    private static FfProbeData BuildFfProbeData(string file, FfProbeRawResult raw)
    {
        List<FfProbeVideoStream> videoStreams = [];
        List<FfProbeAudioStream> audioStreams = [];
        List<FfProbeSubtitleStream> subtitleStreams = [];
        List<FfProbeImageStream> imageStreams = [];

        foreach (FfProbeRawStream s in raw.Streams ?? [])
        {
            string codecType = (s.CodecType ?? "").ToLowerInvariant();

            // mjpeg streams are images, not video
            if (codecType == "video" && s.CodecName == "mjpeg")
                codecType = "image";

            switch (codecType)
            {
                case "video":
                    videoStreams.Add(new()
                    {
                        Index = s.Index,
                        CodecName = s.CodecName,
                        Width = s.Width,
                        Height = s.Height,
                        PixFmt = s.PixFmt,
                        ColorSpace = s.ColorSpace,
                        ColorTransfer = s.ColorTransfer,
                        ColorPrimaries = s.ColorPrimaries,
                        Language = s.Tags?.GetValueOrDefault("language")
                    });
                    break;
                case "audio":
                    audioStreams.Add(new()
                    {
                        Index = s.Index,
                        CodecName = s.CodecName,
                        Language = s.Tags?.GetValueOrDefault("language") ?? "und",
                        Channels = (int)(s.Channels ?? 0),
                        BitRate = s.BitRate ?? 0,
                        SampleRate = (int)(s.SampleRate ?? 0),
                        Tags = s.Tags ?? new()
                    });
                    break;
                case "subtitle":
                    subtitleStreams.Add(new()
                    {
                        Index = s.Index,
                        CodecName = s.CodecName,
                        Language = s.Tags?.GetValueOrDefault("language") ?? "und",
                        Tags = s.Tags ?? new()
                    });
                    break;
                case "image":
                    imageStreams.Add(new()
                    {
                        Index = s.Index,
                        CodecName = s.CodecName,
                        Width = s.Width,
                        Height = s.Height
                    });
                    break;
            }
        }

        TimeSpan duration = TimeSpan.Zero;
        if (raw.Format?.Duration is not null)
        {
            if (double.TryParse(raw.Format.Duration, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                duration = TimeSpan.FromSeconds(seconds);
        }

        FfProbeFormat format = new()
        {
            Filename = raw.Format?.Filename,
            FormatName = raw.Format?.FormatName,
            FormatLongName = raw.Format?.FormatLongName,
            Duration = duration,
            BitRate = long.TryParse(raw.Format?.BitRate, out long br) ? br : 0,
            Tags = raw.Format?.Tags
        };

        return new()
        {
            FilePath = file,
            Duration = duration,
            Format = format,
            VideoStreams = videoStreams,
            AudioStreams = audioStreams,
            SubtitleStreams = subtitleStreams,
            ImageStreams = imageStreams,
            PrimaryVideoStream = videoStreams.Count > 0 ? videoStreams[0] : null,
            PrimaryAudioStream = audioStreams.Count > 0 ? audioStreams[0] : null,
            PrimarySubtitleStream = subtitleStreams.Count > 0 ? subtitleStreams[0] : null,
            PrimaryImageStream = imageStreams.Count > 0 ? imageStreams[0] : null
        };
    }

    private static async Task<string> RunFfprobeWithRetry(string file, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await RunFfprobe(file, ct);
            }
            catch (OperationCanceledException)
            {
                Logger.App($"ffprobe timed out for {file} (attempt {attempt}/{MaxRetries})", LogEventLevel.Warning);
                if (attempt < MaxRetries)
                {
                    await Task.Delay(500, ct);
                    continue;
                }
            }
            catch (Exception ex)
            {
                Logger.App($"ffprobe failed for {file}: {ex.Message} (attempt {attempt}/{MaxRetries})", LogEventLevel.Warning);
                if (attempt < MaxRetries)
                {
                    await Task.Delay(500, ct);
                    continue;
                }
            }
        }

        return string.Empty;
    }

    private static async Task<string> RunFfprobe(string file, CancellationToken ct)
    {
        bool acquired = await FfProbeThrottle.WaitAsync(TimeSpan.FromSeconds(60), ct);
        if (!acquired)
            throw new TimeoutException("Throttle timeout waiting for ffprobe slot");

        Process? process = null;
        try
        {
            using CancellationTokenSource timeoutCts = new(ExecutionTimeoutMs);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            process = new();
            process.StartInfo = new()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = AppFiles.FfProbePath,
                Arguments = $"-hide_banner -v quiet -show_format -show_streams -print_format json \"{file}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true
            };

            process.Start();

            string stdOut = await process.StandardOutput.ReadToEndAsync(linkedCts.Token);

            bool exited = process.WaitForExit(ExecutionTimeoutMs);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                throw new OperationCanceledException("ffprobe did not exit within timeout");
            }

            return stdOut;
        }
        finally
        {
            FfProbeThrottle.Release();
            process?.Dispose();
        }
    }
}

// Internal JSON deserialization types for raw ffprobe output
internal class FfProbeRawResult
{
    [Newtonsoft.Json.JsonProperty("streams")]
    public FfProbeRawStream[]? Streams { get; set; }

    [Newtonsoft.Json.JsonProperty("format")]
    public FfProbeRawFormat? Format { get; set; }
}

internal class FfProbeRawFormat
{
    [Newtonsoft.Json.JsonProperty("filename")]
    public string? Filename { get; set; }

    [Newtonsoft.Json.JsonProperty("format_name")]
    public string? FormatName { get; set; }

    [Newtonsoft.Json.JsonProperty("format_long_name")]
    public string? FormatLongName { get; set; }

    [Newtonsoft.Json.JsonProperty("duration")]
    public string? Duration { get; set; }

    [Newtonsoft.Json.JsonProperty("bit_rate")]
    public string? BitRate { get; set; }

    [Newtonsoft.Json.JsonProperty("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

internal class FfProbeRawStream
{
    [Newtonsoft.Json.JsonProperty("index")]
    public int Index { get; set; }

    [Newtonsoft.Json.JsonProperty("codec_name")]
    public string? CodecName { get; set; }

    [Newtonsoft.Json.JsonProperty("codec_type")]
    public string? CodecType { get; set; }

    [Newtonsoft.Json.JsonProperty("width")]
    public int Width { get; set; }

    [Newtonsoft.Json.JsonProperty("height")]
    public int Height { get; set; }

    [Newtonsoft.Json.JsonProperty("pix_fmt")]
    public string? PixFmt { get; set; }

    [Newtonsoft.Json.JsonProperty("color_space")]
    public string? ColorSpace { get; set; }

    [Newtonsoft.Json.JsonProperty("color_transfer")]
    public string? ColorTransfer { get; set; }

    [Newtonsoft.Json.JsonProperty("color_primaries")]
    public string? ColorPrimaries { get; set; }

    [Newtonsoft.Json.JsonProperty("channels")]
    public long? Channels { get; set; }

    [Newtonsoft.Json.JsonProperty("bit_rate")]
    public long? BitRate { get; set; }

    [Newtonsoft.Json.JsonProperty("sample_rate")]
    public long? SampleRate { get; set; }

    [Newtonsoft.Json.JsonProperty("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
