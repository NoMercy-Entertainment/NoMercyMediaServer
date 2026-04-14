namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

using NoMercy.Encoder.Errors;
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
    private readonly bool _hasGpu;
    private readonly bool _isHdr;

    public EventBusProgressObserver(
        int jobId,
        string title,
        string baseFolder = "",
        string sharePath = "",
        List<string>? videoStreams = null,
        List<string>? audioStreams = null,
        List<string>? subtitleStreams = null,
        bool hasGpu = false,
        bool isHdr = false
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
    }

    public void OnStageStarted(string stageName)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        EventBusProvider
            .Current.PublishAsync(
                new EncodingStageChangedEvent
                {
                    JobId = _jobId,
                    Status = "encoding",
                    Title = _title,
                    Message = $"Stage: {stageName}",
                    BaseFolder = _baseFolder,
                    ShareBasePath = _sharePath,
                    VideoStreams = _videoStreams,
                    AudioStreams = _audioStreams,
                    SubtitleStreams = _subtitleStreams,
                    HasGpu = _hasGpu,
                    IsHdr = _isHdr,
                }
            )
            .GetAwaiter()
            .GetResult();
    }

    public void OnProgress(EncodingProgress progress)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        EventBusProvider
            .Current.PublishAsync(
                new EncodingProgressEvent
                {
                    JobId = _jobId,
                    Percentage = progress.PercentComplete,
                    Elapsed = progress.Elapsed,
                    Estimated = progress.EstimatedRemaining,
                    Fps = progress.CurrentFps,
                    Speed = progress.CurrentSpeed,
                    BitrateKbps = progress.BitrateKbps,
                }
            )
            .GetAwaiter()
            .GetResult();
    }

    public void OnStageCompleted(string stageName, TimeSpan duration)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        EventBusProvider
            .Current.PublishAsync(
                new EncodingStageChangedEvent
                {
                    JobId = _jobId,
                    Status = "encoding",
                    Title = _title,
                    Message = $"Completed: {stageName} ({duration.TotalSeconds:F1}s)",
                    BaseFolder = _baseFolder,
                    ShareBasePath = _sharePath,
                    VideoStreams = _videoStreams,
                    AudioStreams = _audioStreams,
                    SubtitleStreams = _subtitleStreams,
                    HasGpu = _hasGpu,
                    IsHdr = _isHdr,
                }
            )
            .GetAwaiter()
            .GetResult();
    }

    public void OnError(EncodingError error)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        EventBusProvider
            .Current.PublishAsync(
                new EncodingStageChangedEvent
                {
                    JobId = _jobId,
                    Status = "failed",
                    Title = _title,
                    Message = error.Message,
                    BaseFolder = _baseFolder,
                    ShareBasePath = _sharePath,
                }
            )
            .GetAwaiter()
            .GetResult();
    }
}
