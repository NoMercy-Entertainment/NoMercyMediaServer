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

using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.BuildingBlocks;

public interface IThumbnailGenerator
{
    FfmpegCommand BuildCaptureCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        TimeSpan duration
    );

    FfmpegCommand BuildSpriteCommand(
        string ffmpegPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount
    );

    Task WriteVttCueFileAsync(
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount,
        TimeSpan duration,
        CancellationToken ct
    );

    void CleanupIndividualThumbnails(string outputDirectory, ThumbnailOutputPlan plan);
}
