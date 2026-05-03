using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
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

    public static async Task<FfProbeData> CreateAsync(
        IStorageDriver driver,
        string file,
        CancellationToken ct = default
    )
    {
        if (driver is LocalStorageDriver)
            return await CreateAsync(file, ct);

        try
        {
            string json = await RunFfprobeStdinWithRetry(driver, file, ct);
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
            string codecType = s.CodecType.OrEmpty().ToLowerInvariant();

            // mjpeg streams are images, not video
            if (codecType == "video" && s.CodecName == "mjpeg")
                codecType = "image";

            switch (codecType)
            {
                case "video":
                    videoStreams.Add(
                        new()
                        {
                            Index = s.Index,
                            CodecName = s.CodecName,
                            Width = s.Width,
                            Height = s.Height,
                            PixFmt = s.PixFmt,
                            ColorSpace = s.ColorSpace,
                            ColorTransfer = s.ColorTransfer,
                            ColorPrimaries = s.ColorPrimaries,
                            Language = s.Tags?.GetValueOrDefault("language"),
                        }
                    );
                    break;
                case "audio":
                    audioStreams.Add(
                        new()
                        {
                            Index = s.Index,
                            CodecName = s.CodecName,
                            Language = s.Tags?.GetValueOrDefault("language") ?? "und",
                            Channels = (int)(s.Channels ?? 0),
                            BitRate = s.BitRate ?? 0,
                            SampleRate = (int)(s.SampleRate ?? 0),
                            Tags = s.Tags ?? new(),
                        }
                    );
                    break;
                case "subtitle":
                    subtitleStreams.Add(
                        new()
                        {
                            Index = s.Index,
                            CodecName = s.CodecName,
                            Language = s.Tags?.GetValueOrDefault("language") ?? "und",
                            Tags = s.Tags ?? new(),
                        }
                    );
                    break;
                case "image":
                    imageStreams.Add(
                        new()
                        {
                            Index = s.Index,
                            CodecName = s.CodecName,
                            Width = s.Width,
                            Height = s.Height,
                        }
                    );
                    break;
            }
        }

        TimeSpan duration = TimeSpan.Zero;
        if (raw.Format?.Duration is not null)
        {
            if (
                double.TryParse(
                    raw.Format.Duration,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double seconds
                )
            )
                duration = TimeSpan.FromSeconds(seconds);
        }

        FfProbeFormat format = new()
        {
            Filename = raw.Format?.Filename,
            FormatName = raw.Format?.FormatName,
            FormatLongName = raw.Format?.FormatLongName,
            Duration = duration,
            BitRate = long.TryParse(raw.Format?.BitRate, out long br) ? br : 0,
            Tags = raw.Format?.Tags,
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
            PrimaryImageStream = imageStreams.Count > 0 ? imageStreams[0] : null,
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
                Logger.App(
                    $"ffprobe timed out for {file} (attempt {attempt}/{MaxRetries})",
                    LogEventLevel.Warning
                );
                if (attempt < MaxRetries)
                {
                    await Task.Delay(500, ct);
                }
            }
            catch (Exception ex)
            {
                Logger.App(
                    $"ffprobe failed for {file}: {ex.Message} (attempt {attempt}/{MaxRetries})",
                    LogEventLevel.Warning
                );
                if (attempt < MaxRetries)
                {
                    int delayMs = IsResourceExhaustionError(ex) ? 2000 * attempt : 500;
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        return string.Empty;
    }

    private static bool IsResourceExhaustionError(Exception ex)
    {
        // Win32 ERROR_COMMITMENT_LIMIT (paging file too small) and similar resource errors
        return ex is Win32Exception win32Ex
            ? win32Ex.NativeErrorCode is 1455 or 8 // ERROR_COMMITMENT_LIMIT or ERROR_NOT_ENOUGH_MEMORY
            : ex.Message.Contains("paging file", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("wisselbestand", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("not enough memory", StringComparison.OrdinalIgnoreCase);
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
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            process = new();
            process.StartInfo = new()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = AppFiles.FfProbePath,
                Arguments =
                    $"-hide_banner -v quiet -show_format -show_streams -print_format json \"{file}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
            };

            process.Start();

            string stdOut = await process.StandardOutput.ReadToEndAsync(linkedCts.Token);

            bool exited = process.WaitForExit(ExecutionTimeoutMs);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
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

    private static async Task<string> RunFfprobeStdinWithRetry(
        IStorageDriver driver,
        string file,
        CancellationToken ct
    )
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await RunFfprobeStdin(driver, file, ct);
            }
            catch (OperationCanceledException)
            {
                Logger.App(
                    $"ffprobe stdin timed out for {file} (attempt {attempt}/{MaxRetries})",
                    LogEventLevel.Warning
                );
                if (attempt < MaxRetries)
                    await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                Logger.App(
                    $"ffprobe stdin failed for {file}: {ex.Message} (attempt {attempt}/{MaxRetries})",
                    LogEventLevel.Warning
                );
                if (attempt < MaxRetries)
                {
                    int delayMs = IsResourceExhaustionError(ex) ? 2000 * attempt : 500;
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        return string.Empty;
    }

    // Pipes the file's contents through ffprobe stdin. ffprobe reads only what
    // it needs to populate format/streams (typically a few MB of header) and
    // then exits — the stdin pump aborts on the broken pipe rather than
    // streaming the whole multi-GB file across NFS.
    private static async Task<string> RunFfprobeStdin(
        IStorageDriver driver,
        string file,
        CancellationToken ct
    )
    {
        bool acquired = await FfProbeThrottle.WaitAsync(TimeSpan.FromSeconds(60), ct);
        if (!acquired)
            throw new TimeoutException("Throttle timeout waiting for ffprobe slot");

        Process? process = null;
        try
        {
            using CancellationTokenSource timeoutCts = new(ExecutionTimeoutMs);
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            process = new();
            process.StartInfo = new()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = AppFiles.FfProbePath,
                Arguments =
                    "-hide_banner -v quiet -show_format -show_streams -print_format json -i pipe:0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            Task pumpTask = PumpStdinAsync(driver, file, process, linkedCts.Token);

            string stdOut = await stdoutTask;

            try
            {
                await pumpTask;
            }
            catch
            {
                // Pump aborts on broken pipe / process exit — expected.
            }

            bool exited = process.WaitForExit(ExecutionTimeoutMs);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
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

    private static async Task PumpStdinAsync(
        IStorageDriver driver,
        string file,
        Process process,
        CancellationToken ct
    )
    {
        const int BufferSize = 256 * 1024;
        byte[] buffer = new byte[BufferSize];

        await using Stream src = driver.OpenReadIsolated(file);
        Stream stdin = process.StandardInput.BaseStream;

        try
        {
            while (!ct.IsCancellationRequested && !process.HasExited)
            {
                int read = await src.ReadAsync(buffer.AsMemory(0, BufferSize), ct);
                if (read == 0)
                    break;
                await stdin.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            await stdin.FlushAsync(ct);
        }
        catch (IOException)
        {
            // ffprobe closed its stdin — it has what it needs.
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch
            {
                // ignored
            }
        }
    }
}

// Internal JSON deserialization types for raw ffprobe output
internal class FfProbeRawResult
{
    [JsonProperty("streams")]
    public FfProbeRawStream[]? Streams { get; set; }

    [JsonProperty("format")]
    public FfProbeRawFormat? Format { get; set; }
}

internal class FfProbeRawFormat
{
    [JsonProperty("filename")]
    public string? Filename { get; set; }

    [JsonProperty("format_name")]
    public string? FormatName { get; set; }

    [JsonProperty("format_long_name")]
    public string? FormatLongName { get; set; }

    [JsonProperty("duration")]
    public string? Duration { get; set; }

    [JsonProperty("bit_rate")]
    public string? BitRate { get; set; }

    [JsonProperty("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

internal class FfProbeRawStream
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("codec_name")]
    public string? CodecName { get; set; }

    [JsonProperty("codec_type")]
    public string? CodecType { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("pix_fmt")]
    public string? PixFmt { get; set; }

    [JsonProperty("color_space")]
    public string? ColorSpace { get; set; }

    [JsonProperty("color_transfer")]
    public string? ColorTransfer { get; set; }

    [JsonProperty("color_primaries")]
    public string? ColorPrimaries { get; set; }

    [JsonProperty("channels")]
    public long? Channels { get; set; }

    [JsonProperty("bit_rate")]
    public long? BitRate { get; set; }

    [JsonProperty("sample_rate")]
    public long? SampleRate { get; set; }

    [JsonProperty("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
