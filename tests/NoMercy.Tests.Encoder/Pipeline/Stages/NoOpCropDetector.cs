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

using NoMercy.Encoder.ContentAnalysis;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Crop detector stub for tests that need a PlanStage but don't exercise
/// crop detection. Always returns <see cref="CropResult.ShouldCrop"/> =
/// <c>false</c> so no <c>crop=</c> filter is emitted.
/// </summary>
internal sealed class NoOpCropDetector : ICropDetector
{
    public Task<CropResult> DetectAsync(string inputPath, CancellationToken ct) =>
        Task.FromResult(new CropResult(0, 0, 0, 0, false));

    public Task<CropResult> DetectAsync(
        string inputPath,
        Guid? sourceVideoFileId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            new CropResult(0, 0, 0, 0, false, sourceVideoFileId)
        );

    public Task<CropResult> DetectAsync(
        string inputPath,
        Guid? sourceVideoFileId,
        bool? sourceIsHdr,
        CancellationToken ct
    ) =>
        Task.FromResult(
            new CropResult(0, 0, 0, 0, false, sourceVideoFileId)
        );
}
