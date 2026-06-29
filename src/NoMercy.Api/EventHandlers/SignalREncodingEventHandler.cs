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
using NoMercy.NmSystem.SystemCalls;

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
        _subscriptions.Add(eventBus.Subscribe<EncodingStartedEvent>(OnEncodingStarted));
        _subscriptions.Add(eventBus.Subscribe<EncodingProgressUpdatedEvent>(OnEncodingProgress));
        _subscriptions.Add(eventBus.Subscribe<EncodingCompletedEvent>(OnEncodingCompleted));
        _subscriptions.Add(eventBus.Subscribe<EncodingFailedEvent>(OnEncodingFailed));
        _subscriptions.Add(eventBus.Subscribe<EncodingStageChangedEvent>(OnEncodingStageChanged));
        _subscriptions.Add(
            eventBus.Subscribe<EncodingProgressBroadcastedEvent>(OnEncoderProgressBroadcast)
        );
    }

    internal async Task OnEncodingStarted(EncodingStartedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "EncodingStarted",
            "dashboardHub",
            new EncodingStartedDto
            {
                Id = @event.JobId,
                InputPath = @event.InputPath,
                OutputPath = @event.OutputPath,
                ProfileName = @event.ProfileName,
                Timestamp = @event.Timestamp,
            }
        );
        _logger.LogInformation(
            $"Encoding started: Job={@event.JobId}, Profile={@event.ProfileName}"
        );
    }

    internal async Task OnEncodingProgress(
        EncodingProgressUpdatedEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll(
            "EncodingProgress",
            "dashboardHub",
            new EncodingProgressDto
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
            "EncodingCompleted",
            "dashboardHub",
            new EncodingCompletedDto
            {
                Id = @event.JobId,
                OutputPath = @event.OutputPath,
                Duration = @event.Duration.TotalSeconds,
                Timestamp = @event.Timestamp,
            }
        );
        _logger.LogInformation($"Encoding completed: Job={@event.JobId}");
    }

    internal async Task OnEncodingFailed(EncodingFailedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "EncodingFailed",
            "dashboardHub",
            new EncodingFailedDto
            {
                Id = @event.JobId,
                InputPath = @event.InputPath,
                ErrorMessage = @event.ErrorMessage,
                ExceptionType = @event.ExceptionType,
                Timestamp = @event.Timestamp,
            }
        );
        _logger.LogInformation($"Encoding failed: Job={@event.JobId}, Error={@event.ErrorMessage}");
    }

    internal async Task OnEncodingStageChanged(
        EncodingStageChangedEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll(
            "encoder-progress",
            "dashboardHub",
            new EncodingStageChangedDto
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
