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

using Microsoft.Extensions.Logging;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Dispatches to the registered <see cref="IDiscIdentifier"/> whose
/// <see cref="IDiscIdentifier.CanHandle"/> matches the disc type.
/// Returns a <see cref="DiscIdentification"/> with
/// <see cref="DiscIdentification.NeedsManualAssignment"/> = true when no
/// identifier claims the disc type.
///
/// Also exposes <see cref="SearchAsync"/> for manual title lookup (used by
/// the dashboard's search-box endpoint) — delegates to
/// <see cref="VideoDiscIdentifier"/>.
/// </summary>
public sealed class DiscIdentificationService(
    IEnumerable<IDiscIdentifier> identifiers,
    ILogger<DiscIdentificationService> logger
)
{
    private readonly IDiscIdentifier[] _identifiers = identifiers.ToArray();

    public async Task<DiscIdentification> IdentifyAsync(DiscInfo disc, CancellationToken ct)
    {
        IDiscIdentifier? identifier = Array.Find(_identifiers, id => id.CanHandle(disc.Type));

        if (identifier is null)
        {
            logger.LogInformation(
                "No IDiscIdentifier registered for disc type {Type} — returning NeedsManualAssignment",
                disc.Type
            );
            return new DiscIdentification(
                Kind: MediaKind.Movie,
                Candidates: [],
                TopConfidence: 0,
                AutoApply: false,
                NeedsManualAssignment: true
            );
        }

        return await identifier.IdentifyAsync(disc, ct);
    }

    /// <summary>
    /// Manual TMDB title search for the dashboard's search-box. Delegates to
    /// <see cref="VideoDiscIdentifier"/>; returns an empty array when no
    /// VideoDiscIdentifier is registered.
    /// </summary>
    public async Task<DiscCandidate[]> SearchAsync(
        string query,
        MediaType type,
        CancellationToken ct
    )
    {
        VideoDiscIdentifier? videoIdentifier =
            Array.Find(_identifiers, id => id is VideoDiscIdentifier) as VideoDiscIdentifier;

        if (videoIdentifier is null)
        {
            logger.LogInformation(
                "SearchAsync called but no VideoDiscIdentifier is registered — returning empty"
            );
            return [];
        }

        return await videoIdentifier.SearchAsync(query, type, ct);
    }
}
