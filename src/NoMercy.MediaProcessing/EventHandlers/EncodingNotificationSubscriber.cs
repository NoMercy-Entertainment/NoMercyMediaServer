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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Notifications;
using NoMercy.Events;
using NoMercy.Events.Encoding;

namespace NoMercy.MediaProcessing.EventHandlers;

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
            logger.LogDebug(message: "No notification webhook URLs configured; subscriber idle");
            return Task.CompletedTask;
        }

        _subscriptions.Add(
            item: eventBus.Subscribe<EncodingStartedEvent>(
                handler: (evt, ct) =>
                    dispatcher.NotifyStartedAsync(
                        notification: new(JobId: evt.JobId, InputPath: evt.InputPath, OutputPath: evt.OutputPath, ProfileName: evt.ProfileName),
                        ct: ct
                    )
            )
        );

        _subscriptions.Add(
            item: eventBus.Subscribe<EncodingCompletedEvent>(
                handler: (evt, ct) =>
                    dispatcher.NotifyCompletedAsync(
                        notification: new(JobId: evt.JobId, OutputPath: evt.OutputPath, Duration: evt.Duration),
                        ct: ct
                    )
            )
        );

        _subscriptions.Add(
            item: eventBus.Subscribe<EncodingFailedEvent>(
                handler: (evt, ct) =>
                    dispatcher.NotifyFailedAsync(
                        notification: new(JobId: evt.JobId, InputPath: evt.InputPath, ErrorMessage: evt.ErrorMessage, ExceptionType: evt.ExceptionType),
                        ct: ct
                    )
            )
        );

        logger.LogInformation(
            message: "Encoding notification subscriber active — {Count} webhook URL(s) configured",
            args: options.NotificationWebhookUrls.Count
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
                logger.LogWarning(exception: ex, message: "Could not dispose notification subscription");
            }
        }
        _subscriptions.Clear();
        return Task.CompletedTask;
    }
}
