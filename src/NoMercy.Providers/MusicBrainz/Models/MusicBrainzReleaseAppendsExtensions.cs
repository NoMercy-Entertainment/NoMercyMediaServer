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

namespace NoMercy.Providers.MusicBrainz.Models;

public static class MusicBrainzReleaseAppendsExtensions
{
    /// <summary>
    /// The most confident release year available. A specific pressing's own date
    /// is frequently absent — most reliably on reissues and digital-only additions
    /// — even when the work it belongs to has a well-known year, so this falls
    /// back through the release's own events, then the release group's date,
    /// before giving up. Three call sites (folder naming, the stored encode year,
    /// and the library-scan import path) each duplicated this chain and only two
    /// of the three got the release-group fallback added — the third kept
    /// producing "[0000]" folders for releases the other two named correctly.
    /// One shared method so a future fix reaches every caller.
    /// </summary>
    public static int? ResolvedYear(this MusicBrainzReleaseAppends release) =>
        release.DateTime?.Year
        ?? release.ReleaseEvents?.FirstOrDefault()?.DateTime?.Year
        ?? release.MusicBrainzReleaseGroup?.FirstReleaseDate?.Year;
}
