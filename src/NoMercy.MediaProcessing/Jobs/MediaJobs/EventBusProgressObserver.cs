namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Progress;
using NoMercy.Events;
using NoMercy.Events.Encoding;

public class EventBusProgressObserver(int jobId, string title) : IProgressObserver
{
    public void OnStageStarted(string stageName)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        EventBusProvider
            .Current.PublishAsync(
                new EncodingStageChangedEvent
                {
                    JobId = jobId,
                    Status = "encoding",
                    Title = title,
                    Message = $"Stage: {stageName}",
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
                    JobId = jobId,
                    Percentage = progress.PercentComplete,
                    Elapsed = progress.Elapsed,
                    Estimated = progress.EstimatedRemaining,
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
                    JobId = jobId,
                    Status = "encoding",
                    Title = title,
                    Message = $"Completed: {stageName} ({duration.TotalSeconds:F1}s)",
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
                    JobId = jobId,
                    Status = "failed",
                    Title = title,
                    Message = error.Message,
                }
            )
            .GetAwaiter()
            .GetResult();
    }
}
