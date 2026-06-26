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

namespace NoMercy.Encoder.Errors;

/// <summary>
/// Runtime error envelope returned by encoder controllers when a
/// pipeline operation fails. The dashboard renders this as a toast
/// or modal — <see cref="Id"/> looks up the localised template,
/// <see cref="Suggestion"/> drives the action button, and
/// <see cref="Details"/> carries any extra structured context the
/// caller needs (e.g. the GPU device name for
/// <c>gpu_capacity_exhausted</c>).
///
/// <para>Construct via the factories in <c>RuntimeErrors</c> rather
/// than calling this constructor directly — the factories pin the
/// HTTP status code mapping that the controller middleware relies on.</para>
/// </summary>
public sealed record EncoderErrorShape(
    string Id,
    string Message,
    string? Suggestion,
    object? Details
);
