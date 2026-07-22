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

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode.Protocol;
using NoMercy.Storage;

namespace NoMercy.Encoder.LiveTranscode;

public class LiveFfmpegRunner(
    IProcessRunner processRunner,
    EncoderOptions options,
    ILogger<LiveFfmpegRunner> logger,
    IStorage storage,
    INvencSessionCap nvencSessionCap,
    IHardwareCapabilities hardware,
    ICodecResolver codecResolver,
    IResourceBudget resourceBudget,
    ILiveSessionTransport? transport = null
) : ILiveFfmpegRunner
{
    internal const string PlaylistFileName = "index.m3u8";
    internal const string SegmentPrefix = "seg_";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(milliseconds: 500);

    public async Task RunAsync(LiveRunInput input, LiveSession session, CancellationToken ct)
    {
        // Enforce NVENC concurrent session cap before spawning FFmpeg.
        // Hardware-accelerated live encodes use the first GPU device name for
        // the error message; software encodes skip the check.
        bool requiresGpu = input.Quality.IsHardwareAccelerated;
        string gpuName = hardware.Gpus.Count > 0 ? hardware.Gpus[index: 0].Name : "GPU";

        try
        {
            nvencSessionCap.EnforceForGpuEncode(gpuName: gpuName, requiresGpu: requiresGpu);
        }
        catch (EncoderRuntimeException) when (requiresGpu)
        {
            // Every GPU encode slot is occupied — fall back to a software
            // encoder for THIS session instead of failing it outright. A
            // saturated GPU is a capacity problem, not a reason to break an
            // in-progress watch session when the CPU can pick up the slack.
            input = await FallBackToSoftwareAsync(input: input, session: session, ct: ct).ConfigureAwait(continueOnCapturedContext: false);
            requiresGpu = false;
        }

        // Acquire a resource-budget lease for the duration of this live session
        // so the queue scheduler sees GPU/CPU slots as occupied.
        // An audio-only rendition (AAC) is cheap — one thread, no GPU — so a raw
        // source's several language children don't each reserve a full video slot
        // and starve the video encoder.
        int cpuThreads = input.AudioRenditionOnly ? 1 : 2;
        ResourceRequirement requirement = requiresGpu
            ? new(GpuDeviceKey: gpuName, GpuSlots: 1, CpuThreads: 2)
            : new ResourceRequirement(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: cpuThreads);

        // Declared outside the try so the outer finally can always see whether
        // a lease was actually granted. Acquisition now happens INSIDE the try
        // — it used to run before it, so a throw from CreateDirectory /
        // AcquireLocalPath / BuildArguments below skipped the finally entirely
        // and leaked the GPU/CPU budget forever.
        ResourceLease? lease = null;

        try
        {
            lease = await resourceBudget.AcquireAsync(requirement: requirement, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);

            storage.CreateDirectory(path: input.OutputDirectory);

            await using LocalPathLease outputLease = storage.AcquireLocalPath(
                path: input.OutputDirectory
            );

            string[] arguments = BuildArguments(input: input);

            logger.LogInformation(
                message: "Live FFmpeg starting for session {SessionId} → {Dir}", args: [session.SessionId, input.OutputDirectory]
            );

            ProgressParser progressParser = new();
            HashSet<int> pushedSegments = [];
            using CancellationTokenSource stopPolling =
                CancellationTokenSource.CreateLinkedTokenSource(token: ct);

            Task pollingTask = Task.Run(
                function: () => PollForSegmentsAsync(input: input, session: session, seen: pushedSegments, ct: stopPolling.Token),
                cancellationToken: stopPolling.Token
            );

            void OnStdOut(string line)
            {
                FfmpegProgressSnapshot? snapshot = progressParser.FeedLine(line: line);
                if (snapshot is not null && snapshot.Speed > 0)
                {
                    session.SetSpeed(speed: snapshot.Speed);
                }
            }

            try
            {
                ProcessResult result = await processRunner.RunAsync(
                    executable: options.FfmpegPath,
                    arguments: arguments,
                    onStdOut: OnStdOut,
                    onStdErr: null,
                    workingDirectory: outputLease.Path,
                    cancellationToken: ct
                );

                if (!result.IsSuccess && !ct.IsCancellationRequested)
                {
                    logger.LogWarning(
                        message: "Live FFmpeg for session {SessionId} exited with code {Code}. stderr: {StdErr}", args: [session.SessionId, result.ExitCode, Truncate(value: result.StdErr, max: 1000)]
                    );
                    session.SetState(state: LiveSessionState.Error);
                    await PushTranscodeErrorAsync(
                            sessionId: session.SessionId,
                            message: $"FFmpeg exited with code {result.ExitCode}"
                        )
                        .ConfigureAwait(continueOnCapturedContext: false);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation(
                    message: "Live FFmpeg cancelled for session {SessionId}",
                    args: session.SessionId
                );
            }
            catch (Exception ex)
            {
                logger.LogError(exception: ex, message: "Live FFmpeg threw for session {SessionId}", args: session.SessionId);
                session.SetState(state: LiveSessionState.Error);
                await PushTranscodeErrorAsync(sessionId: session.SessionId, message: ex.Message).ConfigureAwait(continueOnCapturedContext: false);
            }
            finally
            {
                try
                {
                    await stopPolling.CancelAsync();
                    await pollingTask.ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        exception: ex,
                        message: "Segment poller for session {SessionId} faulted",
                        args: session.SessionId
                    );
                }

                // Final sweep after the poller stops — the process may have written
                // segments between the last scheduled poll and its exit.
                try
                {
                    PushNewSegments(input: input, session: session, seen: pushedSegments);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        exception: ex,
                        message: "Final segment drain raised for session {SessionId}",
                        args: session.SessionId
                    );
                }
            }
        }
        finally
        {
            if (lease is not null)
                resourceBudget.Release(lease: lease);

            // A bounded run (LiveRunInput.StopPosition set) finished the gap it was
            // spawned to fill, not the file — it reached content an earlier runner
            // generation already produced. Completing the channel here would end
            // the whole session even though playback continues; park it idle
            // instead so a later seek/resume can spawn a fresh runner against the
            // same live channel. An unbounded run (StopPosition null) genuinely
            // ran to EOF, so CompleteIfCurrentRunner's superseded-generation guard
            // applies as before — see LiveSession.CompleteIfCurrentRunner.
            if (input.StopPosition is null)
                session.CompleteIfCurrentRunner(runnerToken: ct);
            else
                session.MarkRunnerIdle(runnerToken: ct);
        }
    }

    // Delegates to LiveFfmpegArgumentBuilder — kept as a static passthrough so
    // existing callers/tests referencing LiveFfmpegRunner.BuildArguments don't
    // need to change. The actual argv construction is a pure function of the
    // input and doesn't need any of this class's runtime dependencies, so it
    // lives in its own file (browser-safe pixel format, GOP/keyframe
    // alignment, rate control, remux/audio-only branching, CustomArguments
    // merge — see LiveFfmpegArgumentBuilder for the reasoning behind each).
    internal static string[] BuildArguments(LiveRunInput input) =>
        LiveFfmpegArgumentBuilder.Build(input: input);

    // Resolves the software equivalent of the requested quality's codec and
    // swaps it in for THIS run when the GPU's NVENC session cap is exhausted.
    // Updates the session's reported CurrentQuality (so API/UI consumers and
    // any later seek/resume respawn see the fallback) and best-effort pushes
    // a QualityChangedMessage so the client knows playback continues at a
    // CPU-encoded quality rather than silently degrading.
    private async Task<LiveRunInput> FallBackToSoftwareAsync(
        LiveRunInput input,
        LiveSession session,
        CancellationToken ct
    )
    {
        ResolvedCodec software = codecResolver.Resolve(
            codec: input.Quality.Codec,
            hardware: hardware,
            preference: EncoderPreference.ForceSoftware
        );

        LiveQuality fallbackQuality = input.Quality with
        {
            Encoder = software.FfmpegEncoderName,
            IsHardwareAccelerated = false,
        };

        session.SetQuality(quality: fallbackQuality);

        logger.LogWarning(
            message: "GPU session cap exhausted for session {SessionId} — falling back to {Encoder}", args: [session.SessionId, fallbackQuality.Encoder]
        );

        await PushQualityChangedAsync(
                sessionId: session.SessionId,
                quality: fallbackQuality,
                reason: QualityChangeReason.GpuFallbackToCpu,
                ct: ct
            )
            .ConfigureAwait(continueOnCapturedContext: false);

        return input with
        {
            Quality = fallbackQuality,
        };
    }

    private async Task PushQualityChangedAsync(
        string sessionId,
        LiveQuality quality,
        QualityChangeReason reason,
        CancellationToken ct
    )
    {
        if (transport is null)
            return;

        QualityChangedMessage message = new(NewQuality: quality, Reason: reason);

        try
        {
            await transport.SendToClientAsync(sessionId: sessionId, message: message, ct: ct).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                exception: ex,
                message: "Transport push failed for QualityChanged on session {SessionId}",
                args: sessionId
            );
        }
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
                PushNewSegments(input: input, session: session, seen: seen);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(
                    exception: ex,
                    message: "Segment poll transient error for session {SessionId}",
                    args: session.SessionId
                );
            }

            try
            {
                await Task.Delay(delay: PollInterval, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void PushNewSegments(LiveRunInput input, LiveSession session, HashSet<int> seen)
    {
        string playlistPath = Path.Combine(path1: input.OutputDirectory, path2: PlaylistFileName);
        if (!storage.Exists(path: playlistPath))
            return;

        IReadOnlyList<(int Index, TimeSpan Duration)> entries = ParsePlaylist(playlistPath: playlistPath);

        foreach ((int index, TimeSpan duration) in entries)
        {
            string segmentFile = Path.Combine(
                path1: input.OutputDirectory,
                path2: $"{SegmentPrefix}{index:D5}.ts"
            );

            if (seen.Contains(item: index))
                continue;

            if (!storage.Exists(path: segmentFile))
            {
                // The m3u8 can reference a segment before the file has finished
                // its atomic rename — wait for the next poll.
                continue;
            }

            long size = 0;
            try
            {
                size = storage.Size(path: segmentFile);
            }
            catch (Exception)
            {
                // Race with rename — size stays 0, pick up real value next time
            }

            // Segments are absolutely indexed (see LiveFfmpegArgumentBuilder), so
            // a segment's start is its index times the target duration — not a sum
            // accumulated from this runner's first segment, which would be wrong
            // for a runner spawned mid-file by a seek.
            TimeSpan startTime = TimeSpan.FromSeconds(value: (double)index * input.SegmentDurationSeconds);
            Segment segment = new(Index: index, StartTime: startTime, Duration: duration, FilePath: segmentFile, SizeBytes: size);
            session.PushSegment(segment: segment);
            seen.Add(item: index);
        }
    }

    internal IReadOnlyList<(int Index, TimeSpan Duration)> ParsePlaylist(string playlistPath)
    {
        List<(int Index, TimeSpan Duration)> entries = [];

        string[] lines;
        try
        {
            using Stream stream = storage.OpenRead(path: playlistPath);
            using StreamReader reader = new(stream: stream, encoding: Encoding.UTF8);
            List<string> lineList = [];
            while (reader.ReadLine() is string rawLine)
                lineList.Add(item: rawLine);
            lines = [.. lineList];
        }
        catch (IOException)
        {
            // File is mid-write; caller retries
            return entries;
        }

        return ParsePlaylistLines(lines: lines);
    }

    internal static IReadOnlyList<(int Index, TimeSpan Duration)> ParsePlaylistLines(string[] lines)
    {
        List<(int Index, TimeSpan Duration)> entries = [];
        TimeSpan? pendingDuration = null;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith(value: "#EXTINF:", comparisonType: StringComparison.Ordinal))
            {
                string payload = line[8..];
                int commaIdx = payload.IndexOf(value: ',');
                string durationToken = commaIdx >= 0 ? payload[..commaIdx] : payload;

                if (
                    double.TryParse(
                        s: durationToken,
                        style: NumberStyles.Float,
                        provider: CultureInfo.InvariantCulture,
                        result: out double seconds
                    )
                )
                {
                    pendingDuration = TimeSpan.FromSeconds(value: seconds);
                }
            }
            else if (pendingDuration is not null && !line.StartsWith(value: '#'))
            {
                int? index = ExtractIndex(segmentLine: line);
                if (index is int idx)
                    entries.Add(item: (idx, pendingDuration.Value));

                pendingDuration = null;
            }
        }

        return entries;
    }

    private static int? ExtractIndex(string segmentLine)
    {
        int prefixIdx = segmentLine.IndexOf(value: SegmentPrefix, comparisonType: StringComparison.Ordinal);
        if (prefixIdx < 0)
            return null;

        int start = prefixIdx + SegmentPrefix.Length;
        int end = segmentLine.IndexOf(value: '.', startIndex: start);
        if (end < 0)
            return null;

        string digits = segmentLine[start..end];
        return int.TryParse(s: digits, provider: CultureInfo.InvariantCulture, result: out int value) ? value : null;
    }

    private static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] : value;

    private async Task PushTranscodeErrorAsync(string sessionId, string message)
    {
        if (transport is null)
            return;

        TranscodeErrorMessage errorMessage = new(
            Kind: EncodingErrorKind.ProcessCrashed,
            Message: message,
            Recoverable: false
        );

        try
        {
            await transport
                .SendToClientAsync(sessionId: sessionId, message: errorMessage, ct: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                exception: ex,
                message: "Transport push failed for TranscodeError on session {SessionId}",
                args: sessionId
            );
        }
    }
}
