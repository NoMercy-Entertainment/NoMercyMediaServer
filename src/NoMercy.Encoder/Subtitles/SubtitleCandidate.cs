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

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Normalized subtitle candidate returned by any acquisition provider.
/// Encoder code works exclusively with this record — never with provider DTOs.
/// </summary>
public record SubtitleCandidate(
    string Provider,
    string Language,
    double Rating,
    int Downloads,
    bool IsTrustedUploader,
    double? Fps,
    string DownloadUrl,
    string Format,
    string? FileName = null,
    string? ReleaseName = null,
    bool HearingImpaired = false,
    string? Uploader = null
);
