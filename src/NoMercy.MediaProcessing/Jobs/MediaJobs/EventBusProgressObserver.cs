namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;
using NoMercy.Events;
using NoMercy.Events.Encoding;

public class EventBusProgressObserver : IProgressObserver
{
    private readonly int _jobId;
    private readonly string _title;
    private readonly string _baseFolder;
    private readonly string _sharePath;
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
        IEncoderProcessRegistry? registry = null
    )
    {
        _jobId = jobId;
        _title = title;
        _baseFolder = baseFolder;
        _sharePath = sharePath;
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

    public void OnStageStarted(string stageName)
    {
        Publish(status: "encoding", message: $"Stage: {stageName}");
    }

    public void OnProgress(EncodingProgress progress)
    {
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

        EventBusProvider
            .Current.PublishAsync(
                new EncoderProgressBroadcastEvent
                {
                    ProgressData = new
                    {
                        id = _jobId,
                        process_id = progress.ProcessId,
                        title = _title,
                        status = "running",
                        message = "Encoding video",
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
                    },
                }
            )
            .GetAwaiter()
            .GetResult();
    }

    public void OnStageCompleted(string stageName, TimeSpan duration)
    {
        Publish(
            status: "encoding",
            message: $"Completed: {stageName} ({duration.TotalSeconds:F1}s)"
        );
    }

    public void OnCompleted()
    {
        _registry?.UnregisterJob(_jobId);
        // Stay in the "encoding" inflight set so the dashboard card stays
        // visible while VideoEncodeJob runs the post-processing phases
        // (history insert, OCR, library refresh). The card is removed by
        // the EncodingCompletedEvent emitted at the very end of the job.
        Publish(status: "encoding", message: "Finalizing");
    }

    public void OnError(EncodingError error)
    {
        _registry?.UnregisterJob(_jobId);
        Publish(status: "failed", message: error.Message);
    }

    private void Publish(string status, string message)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        EventBusProvider
            .Current.PublishAsync(
                new EncoderProgressBroadcastEvent
                {
                    ProgressData = new
                    {
                        id = _jobId,
                        process_id = _jobId,
                        title = _title,
                        value = 0,
                        status,
                        message,
                        progress = 0,
                        speed = 0,
                        has_gpu = _hasGpu,
                        is_hdr = _isHdr,
                        base_folder = _baseFolder,
                        share_path = _sharePath,
                        thumbnails = "",
                        video_streams = _videoStreams,
                        audio_streams = _audioStreams,
                        subtitle_streams = _subtitleStreams,
                    },
                }
            )
            .GetAwaiter()
            .GetResult();
    }
}
