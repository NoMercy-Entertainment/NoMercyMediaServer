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

using NoMercy.Events;
using NoMercy.Events.Encoding;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

/// <summary>
/// Publishes the terminal events that clear a stranded "Running tasks" card.
/// The dashboard adds a card for any encoder-progress event whose status is
/// in-flight (running / paused / encoding), keyed by media id, and removes it
/// only on a non-inflight status over that same channel or a matching
/// <see cref="EncodingFailedEvent"/>. A coordinator phase that fails or throws
/// after children have reported progress would otherwise leave the card frozen
/// on its last message forever, so every abnormal encode exit routes here to
/// emit both terminal signals for the id.
/// </summary>
public static class EncoderCardTerminator
{
    public static async Task PublishFailedAsync(
        int jobId,
        string title,
        string inputPath,
        string message,
        string exceptionType,
        string? posterPath = null,
        string? backdropPath = null
    )
    {
        if (!EventBusProvider.IsConfigured)
            return;

        await EventBusProvider.Current.PublishAsync(
            new EncodingStageChangedEvent
            {
                JobId = jobId,
                Status = "failed",
                Title = title,
                Message = message,
            }
        );

        await EventBusProvider.Current.PublishAsync(
            new EncodingFailedEvent
            {
                JobId = jobId,
                InputPath = inputPath,
                ErrorMessage = message,
                ExceptionType = exceptionType,
                PosterPath = posterPath,
                BackdropPath = backdropPath,
            }
        );
    }
}
