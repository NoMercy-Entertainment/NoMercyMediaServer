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

using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// One identification strategy for a disc type. Implementations are
/// registered via DI and dispatched by
/// <see cref="DiscIdentificationService"/> based on
/// <see cref="CanHandle"/>.
/// </summary>
public interface IDiscIdentifier
{
    /// <summary>
    /// Returns true when this identifier can handle the given disc type.
    /// </summary>
    bool CanHandle(OpticalDiscType type);

    /// <summary>
    /// Identifies the disc and returns ranked candidates with confidence.
    /// </summary>
    Task<DiscIdentification> IdentifyAsync(DiscInfo disc, CancellationToken ct);
}
