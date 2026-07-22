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
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Encoding;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class SignalREncodingEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    private readonly ILogger<SignalREncodingEventHandler> _logger;

    public SignalREncodingEventHandler(
        ILogger<SignalREncodingEventHandler> logger,
        IEventBus eventBus,
        IClientMessenger clientMessenger
    )
    {
        _logger = logger;
        _clientMessenger = clientMessenger;
        _subscriptions.Add(item: eventBus.Subscribe<EncodingStartedEvent>(handler: OnEncodingStarted));
        _subscriptions.Add(item: eventBus.Subscribe<EncodingProgressUpdatedEvent>(handler: OnEncodingProgress));
        _subscriptions.Add(item: eventBus.Subscribe<EncodingCompletedEvent>(handler: OnEncodingCompleted));
        _subscriptions.Add(item: eventBus.Subscribe<EncodingFailedEvent>(handler: OnEncodingFailed));
        _subscriptions.Add(item: eventBus.Subscribe<EncodingStageChangedEvent>(handler: OnEncodingStageChanged));
        _subscriptions.Add(
            item: eventBus.Subscribe<EncodingProgressBroadcastedEvent>(handler: OnEncoderProgressBroadcast)
        );
    }

    internal async Task OnEncodingStarted(EncodingStartedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "EncodingStarted",
            endpoint: "dashboardHub",
            data: new EncodingStartedDto
            {
                Id = @event.JobId,
                InputPath = @event.InputPath,
                OutputPath = @event.OutputPath,
                ProfileName = @event.ProfileName,
                Timestamp = @event.Timestamp,
            }
        );
        _logger.LogInformation(
            message: "Encoding started: Job={JobId}, Profile={ProfileName}", args: [@event.JobId, @event.ProfileName]
        );
    }

    internal async Task OnEncodingProgress(
        EncodingProgressUpdatedEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll(
            name: "EncodingProgress",
            endpoint: "dashboardHub",
            data: new EncodingProgressDto
            {
                Id = @event.JobId,
                Percentage = @event.Percentage,
                Elapsed = @event.Elapsed.TotalSeconds,
                Estimated = @event.Estimated?.TotalSeconds,
                Fps = @event.Fps,
                Speed = @event.Speed,
                BitrateKbps = @event.BitrateKbps,
            }
        );
    }

    internal async Task OnEncodingCompleted(EncodingCompletedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "EncodingCompleted",
            endpoint: "dashboardHub",
            data: new EncodingCompletedDto
            {
                Id = @event.JobId,
                OutputPath = @event.OutputPath,
                Duration = @event.Duration.TotalSeconds,
                Timestamp = @event.Timestamp,
            }
        );
        _logger.LogInformation(message: "Encoding completed: Job={JobId}", args: @event.JobId);
    }

    internal async Task OnEncodingFailed(EncodingFailedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "EncodingFailed",
            endpoint: "dashboardHub",
            data: new EncodingFailedDto
            {
                Id = @event.JobId,
                InputPath = @event.InputPath,
                ErrorMessage = @event.ErrorMessage,
                ExceptionType = @event.ExceptionType,
                Timestamp = @event.Timestamp,
            }
        );
        _logger.LogInformation(
            message: "Encoding failed: Job={JobId}, Error={ErrorMessage}", args: [@event.JobId, @event.ErrorMessage]
        );
    }

    internal async Task OnEncodingStageChanged(
        EncodingStageChangedEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll(
            name: "encoder-progress",
            endpoint: "dashboardHub",
            data: new EncodingStageChangedDto
            {
                Id = @event.JobId,
                Status = @event.Status,
                Title = @event.Title,
                Message = @event.Message,
                BaseFolder = @event.BaseFolder,
                SharePath = @event.ShareBasePath,
                VideoStreams = @event.VideoStreams,
                AudioStreams = @event.AudioStreams,
                SubtitleStreams = @event.SubtitleStreams,
                HasGpu = @event.HasGpu,
                IsHdr = @event.IsHdr,
            }
        );
    }

    internal async Task OnEncoderProgressBroadcast(
        EncodingProgressBroadcastedEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll(name: "encoder-progress", endpoint: "dashboardHub", data: @event.ProgressData);
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
