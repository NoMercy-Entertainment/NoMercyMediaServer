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
/// Spec-shape for one audio output within a variant. The dashboard shows
/// language, codec, and bitrate alongside the video card so users can see
/// the full per-variant stream list at a glance.
/// </summary>
public sealed record AudioTarget(
    int SourceIndex,
    string Codec,
    int Channels,
    int BitrateKbps,
    string? Language
);
