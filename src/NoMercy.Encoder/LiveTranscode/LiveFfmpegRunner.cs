namespace NoMercy.Encoder.LiveTranscode;

using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Infrastructure;

public class LiveFfmpegRunner(
    IProcessRunner processRunner,
    EncoderOptions options,
    ILogger<LiveFfmpegRunner> logger
) : ILiveFfmpegRunner
{
    private const string PlaylistFileName = "index.m3u8";
    private const string SegmentPrefix = "seg_";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public async Task RunAsync(LiveRunInput input, LiveSession session, CancellationToken ct)
    {
        Directory.CreateDirectory(input.OutputDirectory);

        string[] arguments = BuildArguments(input);

        logger.LogInformation(
            "Live FFmpeg starting for session {SessionId} → {Dir}",
            session.SessionId,
            input.OutputDirectory
        );

        ProgressParser progressParser = new();
        HashSet<int> pushedSegments = [];
        using CancellationTokenSource stopPolling = CancellationTokenSource.CreateLinkedTokenSource(
            ct
        );

        Task pollingTask = Task.Run(
            () => PollForSegmentsAsync(input, session, pushedSegments, stopPolling.Token),
            stopPolling.Token
        );

        void OnStdOut(string line)
        {
            FfmpegProgressSnapshot? snapshot = progressParser.FeedLine(line);
            if (snapshot is not null && snapshot.Speed > 0)
            {
                session.SetSpeed(snapshot.Speed);
            }
        }

        try
        {
            ProcessResult result = await processRunner.RunAsync(
                options.FfmpegPath,
                arguments,
                OnStdOut,
                null,
                input.OutputDirectory,
                ct
            );

            if (!result.IsSuccess && !ct.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Live FFmpeg for session {SessionId} exited with code {Code}. stderr: {StdErr}",
                    session.SessionId,
                    result.ExitCode,
                    Truncate(result.StdErr, 1000)
                );
                session.SetState(LiveSessionState.Error);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "Live FFmpeg cancelled for session {SessionId}",
                session.SessionId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Live FFmpeg threw for session {SessionId}", session.SessionId);
            session.SetState(LiveSessionState.Error);
        }
        finally
        {
            try
            {
                stopPolling.Cancel();
                await pollingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Segment poller for session {SessionId} faulted",
                    session.SessionId
                );
            }

            // Final sweep after the poller stops — the process may have written
            // segments between the last scheduled poll and its exit.
            try
            {
                PushNewSegments(input, session, pushedSegments);
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Final segment drain raised for session {SessionId}",
                    session.SessionId
                );
            }

            session.Complete();
        }
    }

    internal static string[] BuildArguments(LiveRunInput input)
    {
        string playlist = Path.Combine(input.OutputDirectory, PlaylistFileName);
        string segmentPattern = Path.Combine(input.OutputDirectory, $"{SegmentPrefix}%05d.ts");

        List<string> args = ["-hide_banner", "-nostats", "-loglevel", "error"];

        if (input.StartPosition > TimeSpan.Zero)
        {
            args.Add("-ss");
            args.Add(FormatSeconds(input.StartPosition.TotalSeconds));
        }

        args.Add("-i");
        args.Add(input.InputPath);

        args.Add("-map");
        args.Add("0:v:0");
        args.Add("-map");
        args.Add("0:a:0?");

        args.Add("-c:v");
        args.Add(input.Quality.Encoder);

        args.Add("-b:v");
        args.Add($"{input.Quality.BitrateKbps}k");

        args.Add("-vf");
        args.Add($"scale={input.Quality.Width}:{input.Quality.Height}");

        args.Add("-c:a");
        args.Add("aac");
        args.Add("-b:a");
        args.Add("128k");
        args.Add("-ac");
        args.Add("2");

        args.Add("-f");
        args.Add("hls");
        args.Add("-hls_time");
        args.Add(input.SegmentDurationSeconds.ToString(CultureInfo.InvariantCulture));
        args.Add("-hls_list_size");
        args.Add("0");
        args.Add("-hls_playlist_type");
        args.Add("event");
        args.Add("-hls_flags");
        args.Add("independent_segments+temp_file");
        args.Add("-hls_segment_filename");
        args.Add(segmentPattern);

        args.Add(playlist);

        args.Add("-progress");
        args.Add("pipe:1");

        return [.. args];
    }

    private async Task PollForSegmentsAsync(
        LiveRunInput input,
        LiveSession session,
        HashSet<int> seen,
        CancellationToken ct
    )
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                PushNewSegments(input, session, seen);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(
                    ex,
                    "Segment poll transient error for session {SessionId}",
                    session.SessionId
                );
            }

            try
            {
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void PushNewSegments(LiveRunInput input, LiveSession session, HashSet<int> seen)
    {
        string playlistPath = Path.Combine(input.OutputDirectory, PlaylistFileName);
        if (!File.Exists(playlistPath))
            return;

        IReadOnlyList<(int Index, TimeSpan Duration)> entries = ParsePlaylist(playlistPath);

        TimeSpan runningStart = TimeSpan.Zero;
        foreach ((int index, TimeSpan duration) in entries)
        {
            string segmentFile = Path.Combine(
                input.OutputDirectory,
                $"{SegmentPrefix}{index:D5}.ts"
            );

            if (seen.Contains(index))
            {
                runningStart += duration;
                continue;
            }

            if (!File.Exists(segmentFile))
            {
                // The m3u8 can reference a segment before the file has finished
                // its atomic rename — wait for the next poll.
                runningStart += duration;
                continue;
            }

            long size = 0;
            try
            {
                size = new FileInfo(segmentFile).Length;
            }
            catch
            {
                // Race with rename — size stays 0, pick up real value next time
            }

            Segment segment = new(index, runningStart, duration, segmentFile, size);
            session.PushSegment(segment);
            seen.Add(index);
            runningStart += duration;
        }
    }

    internal static IReadOnlyList<(int Index, TimeSpan Duration)> ParsePlaylist(string playlistPath)
    {
        List<(int Index, TimeSpan Duration)> entries = [];

        string[] lines;
        try
        {
            lines = File.ReadAllLines(playlistPath);
        }
        catch (IOException)
        {
            // File is mid-write; caller retries
            return entries;
        }

        TimeSpan? pendingDuration = null;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXTINF:", StringComparison.Ordinal))
            {
                string payload = line[8..];
                int commaIdx = payload.IndexOf(',');
                string durationToken = commaIdx >= 0 ? payload[..commaIdx] : payload;

                if (
                    double.TryParse(
                        durationToken,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double seconds
                    )
                )
                {
                    pendingDuration = TimeSpan.FromSeconds(seconds);
                }
            }
            else if (pendingDuration is not null && !line.StartsWith('#'))
            {
                int? index = ExtractIndex(line);
                if (index is int idx)
                    entries.Add((idx, pendingDuration.Value));

                pendingDuration = null;
            }
        }

        return entries;
    }

    private static int? ExtractIndex(string segmentLine)
    {
        int prefixIdx = segmentLine.IndexOf(SegmentPrefix, StringComparison.Ordinal);
        if (prefixIdx < 0)
            return null;

        int start = prefixIdx + SegmentPrefix.Length;
        int end = segmentLine.IndexOf('.', start);
        if (end < 0)
            return null;

        string digits = segmentLine[start..end];
        return int.TryParse(digits, CultureInfo.InvariantCulture, out int value) ? value : null;
    }

    private static string FormatSeconds(double seconds) =>
        seconds.ToString("F3", CultureInfo.InvariantCulture);

    private static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] : value;
}
