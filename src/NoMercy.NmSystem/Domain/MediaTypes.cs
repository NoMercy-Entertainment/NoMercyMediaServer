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

namespace NoMercy.NmSystem.Domain;

public static class MediaTypes
{
    public const string TvMediaType = "tv";
    public const string MovieMediaType = "movie";
    public const string AnimeMediaType = "anime";
    public const string MusicMediaType = "music";
    public const string InboxMediaType = "inbox";
    public const string CollectionMediaType = "collection";
    // Wire/stored value is the plural "specials" everywhere else (all the
    // Special* DTOs, SpecialController, and UserData.Type rows persisted from
    // the client). The singular "special" here silently failed every
    // playlist-type match for specials (SetTime threw "Invalid playlist type",
    // and the special comparisons in VideoPlaybackCommandHandler / UserData
    // repositories never matched).
    public const string SpecialMediaType = "specials";
}
