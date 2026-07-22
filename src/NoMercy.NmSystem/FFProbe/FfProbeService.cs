// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Serilog.Events;

namespace NoMercy.NmSystem.FFProbe;

public class FfProbeService : IFfProbeService
{
    public static FfProbeService Current { get; } = new();

    private const int ExecutionTimeoutMs = 30000;
    private const int MaxRetries = 3;

    public async Task<FfProbeData> CreateAsync(string file, CancellationToken ct = default)
    {
        try
        {
            string json = await RunFfprobeWithRetry(file: file, ct: ct);
            if (string.IsNullOrEmpty(value: json))
                return new() { ErrorData = ["ffprobe returned empty output"] };

            FfProbeRawResult? raw = json.FromJson<FfProbeRawResult>();
            if (raw is null)
                return new() { ErrorData = ["Failed to parse ffprobe output"] };

            return BuildFfProbeData(file: file, raw: raw);
        }
        catch (Exception ex)
        {
            Logger.App(message: $"FfProbe failed for: {file}: {ex.Message}", level: LogEventLevel.Warning);
            return new() { ErrorData = [ex.Message] };
        }
    }

    public async Task<FfProbeData> CreateAsync(
        IStorageDriver driver,
        string file,
        CancellationToken ct = default
    )
    {
        if (driver is LocalStorageDriver)
            return await CreateAsync(file: file, ct: ct);

        // HLS playlists reference segments by relative URI; ffprobe needs URL
        // context to fetch them. stdin pipe has none, so it hangs waiting
        // for segment data. Parse the playlist directly instead.
        if (Path.GetExtension(path: file).Equals(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase))
            return await ParseHlsAsync(driver: driver, masterPath: file, ct: ct);

        try
        {
            // Fast path: pipe the file header through ffprobe stdin. Cheap for
            // faststart / streamable containers — ffprobe reads a few MB and
            // exits without pulling the whole file across the network.
            string json = await RunFfprobeStdinWithRetry(driver: driver, file: file, ct: ct);
            FfProbeRawResult? raw = string.IsNullOrEmpty(value: json)
                ? null
                : json.FromJson<FfProbeRawResult>();

            // The pipe is non-seekable and byte-capped, so it can't handle an
            // imperfect or legacy encode whose metadata it can't reach in one
            // forward pass (moov atom at the end, unusual atom order, a header
            // past the probe budget). ffprobe streams to EOF, times out, and
            // yields nothing. Fall back to a seekable local copy — the same way
            // playback and the encoder already read these files successfully.
            if (
                raw is null
                || (raw.Format is null && (raw.Streams is null || raw.Streams.Length == 0))
            )
                return await CreateFromStagedCopyAsync(driver: driver, file: file, ct: ct);

            return BuildFfProbeData(file: file, raw: raw);
        }
        catch (Exception ex)
        {
            Logger.App(message: $"FfProbe failed for: {file}: {ex.Message}", level: LogEventLevel.Warning);
            return new() { ErrorData = [ex.Message] };
        }
    }

    // Non-seekable-pipe fallback: stage the remote file to a seekable local temp
    // path (the same primitive the encoder uses for remote sources) and probe
    // that. Handles any encode the pipe can't — legacy/imperfect headers, a
    // trailing index — because ffprobe can seek freely in the local copy.
    private async Task<FfProbeData> CreateFromStagedCopyAsync(
        IStorageDriver driver,
        string file,
        CancellationToken ct
    )
    {
        // Staging streams the whole file over the network link, so gate it with
        // the same remote-concurrency cap the pipe probes use — otherwise a scan
        // full of legacy files would saturate the backend the cap protects.
        bool remoteAcquired = await FfProbeThrottle.WaitRemoteAsync(timeout: TimeSpan.FromSeconds(seconds: 120), ct: ct);
        if (!remoteAcquired)
            throw new TimeoutException(message: "Throttle timeout waiting for remote ffprobe slot");

        try
        {
            await using LocalPathLease lease = await driver.AcquireLocalPathAsync(path: file, ct: ct);

            string json = await RunFfprobeWithRetry(file: lease.Path, ct: ct);
            if (string.IsNullOrEmpty(value: json))
                return new() { ErrorData = ["ffprobe returned empty output"] };

            FfProbeRawResult? raw = json.FromJson<FfProbeRawResult>();
            if (raw is null)
                return new() { ErrorData = ["Failed to parse ffprobe output"] };

            return BuildFfProbeData(file: file, raw: raw);
        }
        finally
        {
            FfProbeThrottle.ReleaseRemote();
        }
    }

    private async Task<FfProbeData> ParseHlsAsync(
        IStorageDriver driver,
        string masterPath,
        CancellationToken ct
    )
    {
        try
        {
            string masterText = await ReadAllTextAsync(driver: driver, path: masterPath, ct: ct);

            FfProbeVideoStream? videoStream = null;
            FfProbeAudioStream? audioStream = null;
            string? variantUri = null;
            int? width = null;
            int? height = null;
            string? videoCodec = null;
            string? audioCodec = null;

            string[] lines = masterText.Split(separator: '\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd(trimChar: '\r');
                if (line.StartsWith(value: "#EXT-X-STREAM-INF", comparisonType: StringComparison.Ordinal))
                {
                    Match res = Regex.Match(input: line, pattern: @"RESOLUTION=(\d+)x(\d+)");
                    if (res.Success)
                    {
                        width = int.Parse(s: res.Groups[groupnum: 1].Value);
                        height = int.Parse(s: res.Groups[groupnum: 2].Value);
                    }
                    Match codecs = Regex.Match(input: line, pattern: "CODECS=\"([^\"]+)\"");
                    if (codecs.Success)
                    {
                        string[] codecList = codecs.Groups[groupnum: 1].Value.Split(separator: ',');
                        foreach (string codec in codecList)
                        {
                            string c = codec.Trim();
                            if (c.StartsWith(value: "avc", comparisonType: StringComparison.OrdinalIgnoreCase))
                                videoCodec = "h264";
                            else if (
                                c.StartsWith(value: "hvc", comparisonType: StringComparison.OrdinalIgnoreCase)
                                || c.StartsWith(value: "hev", comparisonType: StringComparison.OrdinalIgnoreCase)
                            )
                                videoCodec = "hevc";
                            else if (c.StartsWith(value: "av01", comparisonType: StringComparison.OrdinalIgnoreCase))
                                videoCodec = "av1";
                            else if (c.StartsWith(value: "mp4a", comparisonType: StringComparison.OrdinalIgnoreCase))
                                audioCodec = "aac";
                            else if (c.StartsWith(value: "opus", comparisonType: StringComparison.OrdinalIgnoreCase))
                                audioCodec = "opus";
                        }
                    }

                    if (i + 1 < lines.Length)
                    {
                        string next = lines[i + 1].TrimEnd(trimChar: '\r').Trim();
                        if (!string.IsNullOrEmpty(value: next) && !next.StartsWith(value: '#'))
                            variantUri = next;
                    }
                    break;
                }
            }

            if (width is not null && height is not null)
                videoStream = new()
                {
                    Index = 0,
                    CodecName = videoCodec,
                    Width = width.Value,
                    Height = height.Value,
                };
            if (audioCodec is not null)
                audioStream = new()
                {
                    Index = 1,
                    CodecName = audioCodec,
                    Language = "und",
                    Tags = new(),
                };

            TimeSpan duration = TimeSpan.Zero;
            if (variantUri is not null)
            {
                string masterDir = Path.GetDirectoryName(path: masterPath)!.Replace(oldChar: '\\', newChar: '/');
                string variantPath = variantUri.StartsWith(value: '/')
                    ? variantUri
                    : masterDir.TrimEnd(trimChar: '/') + "/" + variantUri;
                duration = await SumExtinfDurationAsync(driver: driver, playlistPath: variantPath, ct: ct);
            }

            FfProbeFormat format = new() { Filename = masterPath, Duration = duration };

            return new()
            {
                FilePath = masterPath,
                Duration = duration,
                Format = format,
                VideoStreams = videoStream is not null ? [videoStream] : [],
                AudioStreams = audioStream is not null ? [audioStream] : [],
                SubtitleStreams = [],
                ImageStreams = [],
                PrimaryVideoStream = videoStream,
                PrimaryAudioStream = audioStream,
            };
        }
        catch (Exception ex)
        {
            Logger.App(message: $"HLS parse failed for {masterPath}: {ex.Message}", level: LogEventLevel.Warning);
            return new() { ErrorData = [ex.Message] };
        }
    }

    private async Task<TimeSpan> SumExtinfDurationAsync(
        IStorageDriver driver,
        string playlistPath,
        CancellationToken ct
    )
    {
        if (!driver.FileExists(path: playlistPath))
            return TimeSpan.Zero;

        string text = await ReadAllTextAsync(driver: driver, path: playlistPath, ct: ct);
        double total = 0;
        foreach (string raw in text.Split(separator: '\n'))
        {
            string line = raw.TrimEnd(trimChar: '\r');
            if (!line.StartsWith(value: "#EXTINF:", comparisonType: StringComparison.Ordinal))
                continue;
            string tail = line["#EXTINF:".Length..];
            int comma = tail.IndexOf(value: ',');
            string num = comma >= 0 ? tail[..comma] : tail;
            if (
                double.TryParse(
                    s: num,
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out double secs
                )
            )
                total += secs;
        }
        return TimeSpan.FromSeconds(value: total);
    }

    private async Task<string> ReadAllTextAsync(
        IStorageDriver driver,
        string path,
        CancellationToken ct
    )
    {
        // HLS playlists are small; use the shared driver context (serialized
        // by the driver's own lock) instead of OpenReadIsolated to avoid
        // libnfs NFSv4 session contention across parallel scan workers.
        await using Stream s = driver.OpenRead(path: path);
        using StreamReader reader = new(stream: s);
        return await reader.ReadToEndAsync(cancellationToken: ct);
    }

    private FfProbeData BuildFfProbeData(string file, FfProbeRawResult raw)
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
                        item: new()
                        {
                            Index = s.Index,
                            CodecName = s.CodecName,
                            Width = s.Width,
                            Height = s.Height,
                            PixFmt = s.PixFmt,
                            ColorSpace = s.ColorSpace,
                            ColorTransfer = s.ColorTransfer,
                            ColorPrimaries = s.ColorPrimaries,
                            Language = s.Tags?.GetValueOrDefault(key: "language"),
                        }
                    );
                    break;
                case "audio":
                    audioStreams.Add(
                        item: new()
                        {
                            Index = s.Index,
                            CodecName = s.CodecName,
                            Language = s.Tags?.GetValueOrDefault(key: "language") ?? "und",
                            Channels = (int)(s.Channels ?? 0),
                            BitRate = s.BitRate ?? 0,
                            SampleRate = (int)(s.SampleRate ?? 0),
                            Tags = s.Tags ?? new(),
                        }
                    );
                    break;
                case "subtitle":
                    subtitleStreams.Add(
                        item: new()
                        {
                            Index = s.Index,
                            CodecName = s.CodecName,
                            Language = s.Tags?.GetValueOrDefault(key: "language") ?? "und",
                            Tags = s.Tags ?? new(),
                        }
                    );
                    break;
                case "image":
                    imageStreams.Add(
                        item: new()
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
                    s: raw.Format.Duration,
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out double seconds
                )
            )
                duration = TimeSpan.FromSeconds(value: seconds);
        }

        FfProbeFormat format = new()
        {
            Filename = raw.Format?.Filename,
            FormatName = raw.Format?.FormatName,
            FormatLongName = raw.Format?.FormatLongName,
            Duration = duration,
            BitRate = long.TryParse(s: raw.Format?.BitRate, result: out long br) ? br : 0,
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
            PrimaryVideoStream = videoStreams.Count > 0 ? videoStreams[index: 0] : null,
            PrimaryAudioStream = audioStreams.Count > 0 ? audioStreams[index: 0] : null,
            PrimarySubtitleStream = subtitleStreams.Count > 0 ? subtitleStreams[index: 0] : null,
            PrimaryImageStream = imageStreams.Count > 0 ? imageStreams[index: 0] : null,
        };
    }

    private async Task<string> RunFfprobeWithRetry(string file, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await RunFfprobe(file: file, ct: ct);
            }
            catch (OperationCanceledException)
            {
                Logger.App(
                    message: $"ffprobe timed out for {file} (attempt {attempt}/{MaxRetries})",
                    level: LogEventLevel.Warning
                );
                if (attempt < MaxRetries)
                {
                    await Task.Delay(millisecondsDelay: 500, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Logger.App(
                    message: $"ffprobe failed for {file}: {ex.Message} (attempt {attempt}/{MaxRetries})",
                    level: LogEventLevel.Warning
                );
                if (attempt < MaxRetries)
                {
                    int delayMs = IsResourceExhaustionError(ex: ex) ? 2000 * attempt : 500;
                    await Task.Delay(millisecondsDelay: delayMs, cancellationToken: ct);
                }
            }
        }

        return string.Empty;
    }

    private bool IsResourceExhaustionError(Exception ex)
    {
        // Win32 ERROR_COMMITMENT_LIMIT (paging file too small) and similar resource errors
        return ex is Win32Exception win32Ex
            ? win32Ex.NativeErrorCode is 1455 or 8 // ERROR_COMMITMENT_LIMIT or ERROR_NOT_ENOUGH_MEMORY
            : ex.Message.Contains(value: "paging file", comparisonType: StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains(value: "wisselbestand", comparisonType: StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains(value: "not enough memory", comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> RunFfprobe(string file, CancellationToken ct)
    {
        bool acquired = await FfProbeThrottle.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 60), ct: ct);
        if (!acquired)
            throw new TimeoutException(message: "Throttle timeout waiting for ffprobe slot");

        Process? process = null;
        try
        {
            using CancellationTokenSource timeoutCts = new(millisecondsDelay: ExecutionTimeoutMs);
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(token1: ct, token2: timeoutCts.Token);

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

            string stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken: linkedCts.Token);

            bool exited = process.WaitForExit(milliseconds: ExecutionTimeoutMs);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                throw new OperationCanceledException(message: "ffprobe did not exit within timeout");
            }

            return stdOut;
        }
        finally
        {
            FfProbeThrottle.Release();
            process?.Dispose();
        }
    }

    private async Task<string> RunFfprobeStdinWithRetry(
        IStorageDriver driver,
        string file,
        CancellationToken ct
    )
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await RunFfprobeStdin(driver: driver, file: file, ct: ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // Internal execution timeout, not external cancellation. The pipe
                // can't seek, so retrying it against the same file is futile —
                // return empty and let the caller stage a seekable local copy.
                Logger.App(
                    message: $"ffprobe stdin timed out for {file}; staging a local copy",
                    level: LogEventLevel.Warning
                );
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.App(
                    message: $"ffprobe stdin failed for {file}: {ex.Message} (attempt {attempt}/{MaxRetries})",
                    level: LogEventLevel.Warning
                );
                if (attempt < MaxRetries)
                {
                    int delayMs = IsResourceExhaustionError(ex: ex) ? 2000 * attempt : 500;
                    await Task.Delay(millisecondsDelay: delayMs, cancellationToken: ct);
                }
            }
        }

        return string.Empty;
    }

    // Pipes the file's contents through ffprobe stdin. ffprobe reads only what
    // it needs to populate format/streams (typically a few MB of header) and
    // then exits — the stdin pump aborts on the broken pipe rather than
    // streaming the whole multi-GB file across NFS.
    private async Task<string> RunFfprobeStdin(
        IStorageDriver driver,
        string file,
        CancellationToken ct
    )
    {
        // Remote gate first: caps concurrent network-streamed probes far below
        // the general limit so parallel readers don't saturate the NFS/SMB/S3
        // link and time each other out. Held for the whole probe, released in
        // finally alongside the general slot.
        bool remoteAcquired = await FfProbeThrottle.WaitRemoteAsync(timeout: TimeSpan.FromSeconds(seconds: 120), ct: ct);
        if (!remoteAcquired)
            throw new TimeoutException(message: "Throttle timeout waiting for remote ffprobe slot");

        bool acquired = await FfProbeThrottle.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 60), ct: ct);
        if (!acquired)
        {
            FfProbeThrottle.ReleaseRemote();
            throw new TimeoutException(message: "Throttle timeout waiting for ffprobe slot");
        }

        Process? process = null;
        try
        {
            using CancellationTokenSource timeoutCts = new(millisecondsDelay: ExecutionTimeoutMs);
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(token1: ct, token2: timeoutCts.Token);

            process = new();
            process.StartInfo = new()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = AppFiles.FfProbePath,
                Arguments =
                    // Cap probe budget: stdin can't seek, so without a hard limit
                    // ffprobe reads to EOF when it can't find duration in the
                    // header. 5 MB / 5 s is plenty for any container's format
                    // and stream metadata; missing scan-derived stats are
                    // acceptable for filelist (we only need codecs/resolution/
                    // duration-from-header).
                    "-hide_banner -v quiet -probesize 5M -analyzeduration 5M -show_format -show_streams -print_format json -i pipe:0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken: linkedCts.Token);
            Task pumpTask = PumpStdinAsync(driver: driver, file: file, process: process, ct: linkedCts.Token);

            // Observe the pump no matter which task settles first. If stdoutTask
            // faults/cancels (probe timeout) we must still await pumpTask, or its
            // exception — e.g. an NFS OpenReadIsolated IOException (BAD_SEQID) —
            // escapes as an UnobservedTaskException and is rethrown on the
            // finalizer thread, crashing the whole scan. ObservePumpAsync always
            // awaits and swallows the pump's expected teardown faults.
            string stdOut;
            try
            {
                stdOut = await stdoutTask;
            }
            finally
            {
                await ObservePumpAsync(pumpTask: pumpTask);
            }

            bool exited = process.WaitForExit(milliseconds: ExecutionTimeoutMs);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                throw new OperationCanceledException(message: "ffprobe did not exit within timeout");
            }

            return stdOut;
        }
        finally
        {
            FfProbeThrottle.Release();
            FfProbeThrottle.ReleaseRemote();
            process?.Dispose();
        }
    }

    // Await the stdin pump and swallow its expected teardown faults so the
    // task is always observed. A pump fault is never fatal to the probe: the
    // source read may fail (NFS BAD_SEQID / timeout / broken pipe) or ffprobe
    // may close stdin early once it has the header it needs. The caller still
    // returns whatever ffprobe wrote to stdout.
    private static async Task ObservePumpAsync(Task pumpTask)
    {
        try
        {
            await pumpTask;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // Broken pipe / cancellation / NFS read failure — expected pump teardown.
        }
    }

    private async Task PumpStdinAsync(
        IStorageDriver driver,
        string file,
        Process process,
        CancellationToken ct
    )
    {
        const int BufferSize = 256 * 1024;
        byte[] buffer = new byte[BufferSize];

        // Open before the try so a failure to open (e.g. NFS BAD_SEQID) still
        // closes stdin in the finally — otherwise ffprobe blocks forever waiting
        // on pipe:0. ObservePumpAsync observes the rethrown IOException.
        Stream src;
        try
        {
            src = driver.OpenReadIsolated(path: file);
        }
        catch
        {
            try
            {
                process.StandardInput.Close();
            }
            catch
            {
                // ignored
            }
            throw;
        }

        await using (src)
        {
            Stream stdin = process.StandardInput.BaseStream;

            try
            {
                while (!ct.IsCancellationRequested && !process.HasExited)
                {
                    int read = await src.ReadAsync(buffer: buffer.AsMemory(start: 0, length: BufferSize), cancellationToken: ct);
                    if (read == 0)
                        break;
                    await stdin.WriteAsync(buffer: buffer.AsMemory(start: 0, length: read), cancellationToken: ct);
                }
                await stdin.FlushAsync(cancellationToken: ct);
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
}

// Internal JSON deserialization types for raw ffprobe output
internal class FfProbeRawResult
{
    [JsonProperty(propertyName: "streams")]
    public FfProbeRawStream[]? Streams { get; set; }

    [JsonProperty(propertyName: "format")]
    public FfProbeRawFormat? Format { get; set; }
}

internal class FfProbeRawFormat
{
    [JsonProperty(propertyName: "filename")]
    public string? Filename { get; set; }

    [JsonProperty(propertyName: "format_name")]
    public string? FormatName { get; set; }

    [JsonProperty(propertyName: "format_long_name")]
    public string? FormatLongName { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string? Duration { get; set; }

    [JsonProperty(propertyName: "bit_rate")]
    public string? BitRate { get; set; }

    [JsonProperty(propertyName: "tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

internal class FfProbeRawStream
{
    [JsonProperty(propertyName: "index")]
    public int Index { get; set; }

    [JsonProperty(propertyName: "codec_name")]
    public string? CodecName { get; set; }

    [JsonProperty(propertyName: "codec_type")]
    public string? CodecType { get; set; }

    [JsonProperty(propertyName: "width")]
    public int Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public int Height { get; set; }

    [JsonProperty(propertyName: "pix_fmt")]
    public string? PixFmt { get; set; }

    [JsonProperty(propertyName: "color_space")]
    public string? ColorSpace { get; set; }

    [JsonProperty(propertyName: "color_transfer")]
    public string? ColorTransfer { get; set; }

    [JsonProperty(propertyName: "color_primaries")]
    public string? ColorPrimaries { get; set; }

    [JsonProperty(propertyName: "channels")]
    public long? Channels { get; set; }

    [JsonProperty(propertyName: "bit_rate")]
    public long? BitRate { get; set; }

    [JsonProperty(propertyName: "sample_rate")]
    public long? SampleRate { get; set; }

    [JsonProperty(propertyName: "tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
