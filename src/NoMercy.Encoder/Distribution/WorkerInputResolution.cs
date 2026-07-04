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
namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Outcome of resolving a signed worker task payload into a dispatch-ready
/// <see cref="EncodeTask"/>. Carries enough state for the caller to map each
/// failure mode onto the right transport response without re-deriving it.
/// </summary>
public sealed record WorkerInputResolution
{
    /// <summary>
    /// The deserialized task, or <c>null</c> when the payload failed HMAC
    /// verification or had expired.
    /// </summary>
    public EncodeTask? Task { get; init; }

    /// <summary>
    /// The task with its input path rewritten to the locally-fetched source,
    /// or <c>null</c> when the source could not be made local.
    /// </summary>
    public EncodeTask? EffectiveTask { get; init; }

    /// <summary>True when the source could not be fetched or made local.</summary>
    public bool SourceFetchFailed { get; init; }

    /// <summary>
    /// The source-fetch error message, set when <see cref="SourceFetchFailed"/>
    /// is <c>true</c>.
    /// </summary>
    public string? SourceFetchError { get; init; }
}
