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

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;
using NoMercy.Events;
using NoMercy.Events.Encoding;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

internal static class EventBusFireAndForget
{
    public static void Publish(EncodingProgressBroadcastedEvent evt)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        // Fire-and-forget. The previous .GetAwaiter().GetResult() blocked the
        // ffmpeg-output reader thread on every snapshot — at 500ms throttle
        // × N concurrent encodes that single line contributed measurably to
        // server CPU during heavy queues. Dropped publishes self-heal on the
        // next progress tick; faults are observed via ContinueWith so the
        // task scheduler doesn't surface them as UnobservedTaskException.
        _ = EventBusProvider
            .Current.PublishAsync(evt)
            .ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }
}

public class EventBusProgressObserver : IProgressObserver, IDisposable
{
    // The dashboard drops a card that has gone quiet, on the assumption that a
    // live encode never stops talking. Only ffmpeg talks that often: a stage
    // announces itself once and then works in silence, and Plan in particular
    // runs crop detection and the encode-yield probe back to back — twenty-odd
    // seconds on a 24-minute source. Every one of those stages outlived the
    // card it had just created, so the panel went blank precisely while the
    // server was busy. This re-states the current stage until it ends, which
    // also gives the card a running clock instead of a frozen label.
    private static readonly TimeSpan StageHeartbeatInterval = TimeSpan.FromSeconds(3);

    private readonly Lock _stageLock = new();
    private Timer? _stageHeartbeat;
    private string? _currentStage;
    private DateTime _currentStageStartedAt;

    private readonly int _jobId;
    private readonly string _title;
    private readonly string _baseFolder;
    private readonly string _sharePath;

    // The artwork the dashboard shows beside a running job. Live sprite capture used to fill
    // that space and no longer exists, so `thumbnails` has published an empty string ever
    // since — the item's own backdrop (or an episode's still) is what is left, and it is a
    // better answer anyway: it says which title is encoding rather than which frame.
    private readonly string? _backdrop;
    private readonly List<string> _videoStreams;
    private readonly List<string> _audioStreams;
    private readonly List<string> _subtitleStreams;
    private readonly IEncoderProcessRegistry? _registry;
    private bool _hasGpu;
    private bool _isHdr;
    private int _lastRegisteredPid;

    public EventBusProgressObserver(
        int jobId,
        string title,
        string baseFolder = "",
        string sharePath = "",
        List<string>? videoStreams = null,
        List<string>? audioStreams = null,
        List<string>? subtitleStreams = null,
        bool hasGpu = false,
        bool isHdr = false,
        IEncoderProcessRegistry? registry = null,
        string? backdrop = null
    )
    {
        _jobId = jobId;
        _title = title;
        _baseFolder = baseFolder;
        _sharePath = sharePath;
        _backdrop = backdrop;
        _videoStreams = videoStreams ?? [];
        _audioStreams = audioStreams ?? [];
        _subtitleStreams = subtitleStreams ?? [];
        _hasGpu = hasGpu;
        _isHdr = isHdr;
        _registry = registry;
    }

    public void OnPlanResolved(
        List<string> videoStreams,
        List<string> audioStreams,
        List<string> subtitleStreams,
        bool hasGpu,
        bool isHdr
    )
    {
        _videoStreams.Clear();
        _videoStreams.AddRange(videoStreams);
        _audioStreams.Clear();
        _audioStreams.AddRange(audioStreams);
        _subtitleStreams.Clear();
        _subtitleStreams.AddRange(subtitleStreams);
        _hasGpu = hasGpu;
        _isHdr = isHdr;
    }

    /// <summary>
    /// What this run is actually encoding, from the streams the plan resolved to.
    /// A decomposed bundle carries only its slice — an audio-only top-up has no
    /// video streams — so a fixed "Encoding video" lied on the dashboard whenever
    /// the run was not, in fact, encoding video.
    /// </summary>
    private string EncodeActivityMessage()
    {
        if (_videoStreams.Count > 0)
            return "Encoding video";
        if (_audioStreams.Count > 0)
            return "Encoding audio";
        if (_subtitleStreams.Count > 0)
            return "Encoding subtitles";
        return "Encoding";
    }

    public void OnStageStarted(string stageName)
    {
        BeginStageHeartbeat(stageName);
        Publish(status: "encoding", message: $"Stage: {stageName}");
    }

    private void BeginStageHeartbeat(string stageName)
    {
        lock (_stageLock)
        {
            _currentStage = stageName;
            _currentStageStartedAt = DateTime.UtcNow;

            _stageHeartbeat ??= new(
                _ => PublishStageHeartbeat(),
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan
            );
            _stageHeartbeat.Change(StageHeartbeatInterval, StageHeartbeatInterval);
        }
    }

    private void StopStageHeartbeat()
    {
        lock (_stageLock)
        {
            _currentStage = null;
            _stageHeartbeat?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void PublishStageHeartbeat()
    {
        string stageName;
        TimeSpan elapsed;

        lock (_stageLock)
        {
            if (_currentStage is null)
                return;
            stageName = _currentStage;
            elapsed = DateTime.UtcNow - _currentStageStartedAt;
        }

        Publish(status: "encoding", message: $"Stage: {stageName}", elapsedSeconds: elapsed.TotalSeconds);
    }

    public void OnProgress(EncodingProgress progress)
    {
        // ffmpeg reports on its own cadence; the stage heartbeat would only
        // interleave stale "Stage: Encode" lines with live percentages.
        StopStageHeartbeat();

        // Register the ffmpeg PID on first observation so pause/resume can find it.
        if (
            _registry is not null
            && progress.ProcessId > 0
            && progress.ProcessId != _lastRegisteredPid
        )
        {
            _registry.Register(_jobId, progress.ProcessId);
            _lastRegisteredPid = progress.ProcessId;
        }

        if (!EventBusProvider.IsConfigured)
            return;

        TimeSpan remaining = progress.EstimatedRemaining ?? TimeSpan.Zero;

        EventBusFireAndForget.Publish(
            new()
            {
                ProgressData = new
                {
                    id = _jobId,
                    process_id = progress.ProcessId,
                    title = _title,
                    status = "running",
                    message = EncodeActivityMessage(),
                    progress = progress.PercentComplete,
                    speed = progress.CurrentSpeed ?? 0,
                    fps = progress.CurrentFps ?? 0,
                    frame = 0,
                    bitrate = progress.Bitrate ?? "N/A",
                    current_time = progress.CurrentTimeSeconds,
                    duration = progress.DurationSeconds,
                    remaining = remaining.TotalSeconds,
                    remaining_hms = $"{remaining.Days}:{(int)remaining.TotalHours % 24:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}",
                    remaining_split = new[]
                    {
                        remaining.Days,
                        (int)remaining.TotalHours % 24,
                        remaining.Minutes,
                        remaining.Seconds,
                    },
                    has_gpu = _hasGpu,
                    is_hdr = _isHdr,
                    base_folder = _baseFolder,
                    share_path = _sharePath,
                    video_streams = _videoStreams,
                    audio_streams = _audioStreams,
                    subtitle_streams = _subtitleStreams,
                    thumbnails = "",
                    backdrop = _backdrop,
                },
            }
        );
    }

    public void OnStageCompleted(string stageName, TimeSpan duration)
    {
        StopStageHeartbeat();


        Publish(
            status: "encoding",
            message: $"Completed: {stageName}",
            elapsedSeconds: duration.TotalSeconds
        );
    }

    public void OnCompleted()
    {
        StopStageHeartbeat();
        _registry?.UnregisterJob(_jobId);
        // Stay in the "encoding" inflight set so the dashboard card stays
        // visible while VideoEncodeJob runs the post-processing phases
        // (history insert, OCR, library refresh). The card is removed by
        // the EncodingCompletedEvent emitted at the very end of the job.
        Publish(status: "encoding", message: "Finalizing");
    }

    public void OnError(EncodingError error)
    {
        StopStageHeartbeat();
        _registry?.UnregisterJob(_jobId);
        Publish(status: "failed", message: error.Message);
    }

    public void Dispose()
    {
        lock (_stageLock)
        {
            _currentStage = null;
            _stageHeartbeat?.Dispose();
            _stageHeartbeat = null;
        }

        GC.SuppressFinalize(this);
    }

    private void Publish(string status, string message, double? elapsedSeconds = null)
    {
        EventBusFireAndForget.Publish(
            new()
            {
                ProgressData = new
                {
                    id = _jobId,
                    process_id = _jobId,
                    title = _title,
                    value = 0,
                    status,
                    message,
                    // The card's own dedicated numbers row renders this — the
                    // message above stays a clean stage/phase label instead of
                    // carrying a baked-in "(18s)" that grew every heartbeat.
                    elapsed_seconds = elapsedSeconds,
                    progress = 0,
                    speed = 0,
                    has_gpu = _hasGpu,
                    is_hdr = _isHdr,
                    base_folder = _baseFolder,
                    share_path = _sharePath,
                    thumbnails = "",
                    backdrop = _backdrop,
                    video_streams = _videoStreams,
                    audio_streams = _audioStreams,
                    subtitle_streams = _subtitleStreams,
                },
            }
        );
    }
}
