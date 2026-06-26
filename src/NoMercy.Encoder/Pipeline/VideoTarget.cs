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

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Spec-shape for one video output variant. The dashboard renders each
/// <see cref="VideoTarget"/> as a card in the encoding plan preview so
/// users can confirm codec, resolution, and rate-control before a long
/// encode starts. Populated by <see cref="PlanResultProjector"/>; richer
/// fields (EncoderHandle, GpuIndex, FilterChain) fill in during Phase 3
/// when ExecutionPlan carries hardware-binding information.
/// </summary>
public sealed record VideoTarget(
    string Codec,
    string EncoderHandle,
    int? GpuIndex,
    int Width,
    int Height,
    RateControl RateControl,
    string Preset,
    string Profile,
    string Level,
    string PixelFormat,
    IReadOnlyList<string> FilterChain
);
