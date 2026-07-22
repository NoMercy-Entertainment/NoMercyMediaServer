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

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;

namespace NoMercy.Encoder.Notifications;

/// <summary>
/// Posts one JSON payload per configured URL per encoder event. Retries each
/// URL up to 3 times with exponential backoff (1s, 2s, 4s). Errors are
/// logged and swallowed — notifications never fail an encode.
///
/// Payload shape is stable across the three event types. Consumers should
/// dispatch on the <c>event</c> field.
/// </summary>
public class WebhookNotificationDispatcher(
    EncoderOptions options,
    HttpClient httpClient,
    ILogger<WebhookNotificationDispatcher> logger
) : INotificationDispatcher
{
    private const int MaxRetries = 3;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    public Task NotifyStartedAsync(
        EncodingStartedNotification notification,
        CancellationToken ct
    ) =>
        DispatchAsync(
            eventName: "encoding.started",
            payload: new
            {
                job_id = notification.JobId,
                input_path = notification.InputPath,
                output_path = notification.OutputPath,
                profile_name = notification.ProfileName,
            },
            ct: ct
        );

    public Task NotifyCompletedAsync(
        EncodingCompletedNotification notification,
        CancellationToken ct
    ) =>
        DispatchAsync(
            eventName: "encoding.completed",
            payload: new
            {
                job_id = notification.JobId,
                output_path = notification.OutputPath,
                duration_seconds = notification.Duration.TotalSeconds,
            },
            ct: ct
        );

    public Task NotifyFailedAsync(EncodingFailedNotification notification, CancellationToken ct) =>
        DispatchAsync(
            eventName: "encoding.failed",
            payload: new
            {
                job_id = notification.JobId,
                input_path = notification.InputPath,
                error_message = notification.ErrorMessage,
                exception_type = notification.ExceptionType,
            },
            ct: ct
        );

    private async Task DispatchAsync(string eventName, object payload, CancellationToken ct)
    {
        IList<string> urls = options.NotificationWebhookUrls;
        if (urls.Count == 0)
            return;

        string body = JsonSerializer.Serialize(
            value: new
            {
                @event = eventName,
                timestamp = DateTime.UtcNow.ToString(format: "O"),
                payload,
            },
            options: JsonOptions
        );

        foreach (string url in urls)
        {
            await PostWithRetryAsync(url: url, body: body, ct: ct);
        }
    }

    private async Task PostWithRetryAsync(string url, string body, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            if (ct.IsCancellationRequested)
                return;

            try
            {
                using StringContent content = new(content: body, encoding: Encoding.UTF8, mediaType: "application/json");
                using HttpResponseMessage response = await httpClient.PostAsync(requestUri: url, content: content, cancellationToken: ct);
                if (response.IsSuccessStatusCode)
                    return;

                logger.LogWarning(
                    message: "Webhook {Url} returned {Status} on attempt {Attempt}/{Max}", args: [url, (int)response.StatusCode, attempt, MaxRetries]
                );
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    exception: ex,
                    message: "Webhook {Url} threw on attempt {Attempt}/{Max}", args: [url, attempt, MaxRetries]
                );
            }

            if (attempt < MaxRetries)
            {
                TimeSpan delay = TimeSpan.FromSeconds(value: Math.Pow(x: 2, y: attempt - 1));
                try
                {
                    await Task.Delay(delay: delay, cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        logger.LogWarning(message: "Webhook {Url} exhausted {Max} retries — giving up", args: [url, MaxRetries]);
    }
}
