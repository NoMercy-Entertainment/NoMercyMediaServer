using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class SignalREncodingEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public SignalREncodingEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(eventBus.Subscribe<EncodingStartedEvent>(OnEncodingStarted));
        _subscriptions.Add(eventBus.Subscribe<EncodingProgressEvent>(OnEncodingProgress));
        _subscriptions.Add(eventBus.Subscribe<EncodingCompletedEvent>(OnEncodingCompleted));
        _subscriptions.Add(eventBus.Subscribe<EncodingFailedEvent>(OnEncodingFailed));
        _subscriptions.Add(eventBus.Subscribe<EncodingStageChangedEvent>(OnEncodingStageChanged));
        _subscriptions.Add(
            eventBus.Subscribe<EncoderProgressBroadcastEvent>(OnEncoderProgressBroadcast)
        );
    }

    internal async Task OnEncodingStarted(EncodingStartedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "EncodingStarted",
            "dashboardHub",
            new
            {
                id = @event.JobId,
                input_path = @event.InputPath,
                output_path = @event.OutputPath,
                profile_name = @event.ProfileName,
                timestamp = @event.Timestamp,
            }
        );

        Logger.Socket($"Encoding started: Job={@event.JobId}, Profile={@event.ProfileName}");
    }

    internal async Task OnEncodingProgress(EncodingProgressEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "EncodingProgress",
            "dashboardHub",
            new
            {
                id = @event.JobId,
                percentage = @event.Percentage,
                elapsed = @event.Elapsed.TotalSeconds,
                estimated = @event.Estimated?.TotalSeconds,
                fps = @event.Fps,
                speed = @event.Speed,
                bitrate_kbps = @event.BitrateKbps,
            }
        );
    }

    internal async Task OnEncodingCompleted(EncodingCompletedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "EncodingCompleted",
            "dashboardHub",
            new
            {
                id = @event.JobId,
                output_path = @event.OutputPath,
                duration = @event.Duration.TotalSeconds,
                timestamp = @event.Timestamp,
            }
        );

        Logger.Socket($"Encoding completed: Job={@event.JobId}");
    }

    internal async Task OnEncodingFailed(EncodingFailedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "EncodingFailed",
            "dashboardHub",
            new
            {
                id = @event.JobId,
                input_path = @event.InputPath,
                error_message = @event.ErrorMessage,
                exception_type = @event.ExceptionType,
                timestamp = @event.Timestamp,
            }
        );

        Logger.Socket($"Encoding failed: Job={@event.JobId}, Error={@event.ErrorMessage}");
    }

    internal async Task OnEncodingStageChanged(
        EncodingStageChangedEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll(
            "encoder-progress",
            "dashboardHub",
            new
            {
                id = @event.JobId,
                status = @event.Status,
                title = @event.Title,
                message = @event.Message,
                base_folder = @event.BaseFolder,
                share_path = @event.ShareBasePath,
                video_streams = @event.VideoStreams,
                audio_streams = @event.AudioStreams,
                subtitle_streams = @event.SubtitleStreams,
                has_gpu = @event.HasGpu,
                is_hdr = @event.IsHdr,
            }
        );
    }

    internal async Task OnEncoderProgressBroadcast(
        EncoderProgressBroadcastEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll("encoder-progress", "dashboardHub", @event.ProgressData);
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }
}
