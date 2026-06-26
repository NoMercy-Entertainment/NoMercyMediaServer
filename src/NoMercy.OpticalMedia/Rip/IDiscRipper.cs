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

using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Rip;

/// <summary>
/// Rips one or more titles from an optical disc to intermediate MKV files
/// on local disk. The files can then be fed into the regular encoding
/// pipeline (single-pass, 2-pass, auto-ladder) as a normal video source.
/// </summary>
public interface IDiscRipper
{
    /// <summary>
    /// Rip the titles listed in <paramref name="request"/>. Returns one
    /// <see cref="DiscRipResult"/> per selected title; entries where
    /// <see cref="DiscRipResult.Success"/> is false include a message.
    /// </summary>
    Task<DiscRipResult[]> RipAsync(
        RipRequest request,
        string outputDirectory,
        CancellationToken ct
    );
}

public record DiscRipResult(
    int TitleIndex,
    string OutputPath,
    bool Success,
    TimeSpan Duration,
    long OutputSizeBytes,
    string? Error
);
