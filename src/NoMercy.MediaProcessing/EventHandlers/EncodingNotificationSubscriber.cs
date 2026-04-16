namespace NoMercy.MediaProcessing.EventHandlers;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Notifications;
using NoMercy.Events;
using NoMercy.Events.Encoding;

/// <summary>
/// Wires the encoder event bus to the <see cref="INotificationDispatcher"/>
/// so external webhooks (or any plugin-supplied dispatcher) receive a payload
/// on every encoder lifecycle event. Hosted service lifecycle so subscriptions
/// get disposed on shutdown.
/// </summary>
public class EncodingNotificationSubscriber(
    IEventBus eventBus,
    INotificationDispatcher dispatcher,
    EncoderOptions options,
    ILogger<EncodingNotificationSubscriber> logger
) : IHostedService
{
    private readonly List<IDisposable> _subscriptions = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // No URLs configured → skip subscription entirely. Keeps the hosted
        // service free when nobody's listening.
        if (options.NotificationWebhookUrls.Count == 0)
        {
            logger.LogDebug("No notification webhook URLs configured; subscriber idle");
            return Task.CompletedTask;
        }

        _subscriptions.Add(
            eventBus.Subscribe<EncodingStartedEvent>(
                (evt, ct) =>
                    dispatcher.NotifyStartedAsync(
                        new EncodingStartedNotification(
                            evt.JobId,
                            evt.InputPath,
                            evt.OutputPath,
                            evt.ProfileName
                        ),
                        ct
                    )
            )
        );

        _subscriptions.Add(
            eventBus.Subscribe<EncodingCompletedEvent>(
                (evt, ct) =>
                    dispatcher.NotifyCompletedAsync(
                        new EncodingCompletedNotification(evt.JobId, evt.OutputPath, evt.Duration),
                        ct
                    )
            )
        );

        _subscriptions.Add(
            eventBus.Subscribe<EncodingFailedEvent>(
                (evt, ct) =>
                    dispatcher.NotifyFailedAsync(
                        new EncodingFailedNotification(
                            evt.JobId,
                            evt.InputPath,
                            evt.ErrorMessage,
                            evt.ExceptionType
                        ),
                        ct
                    )
            )
        );

        logger.LogInformation(
            "Encoding notification subscriber active — {Count} webhook URL(s) configured",
            options.NotificationWebhookUrls.Count
        );
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not dispose notification subscription");
            }
        }
        _subscriptions.Clear();
        return Task.CompletedTask;
    }
}
