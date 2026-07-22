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

using Newtonsoft.Json;

namespace NoMercy.MediaProcessing.Inbox;

public sealed record InboxAudioTags
{
    [JsonProperty(propertyName: "music_brainz_release_id")]
    public Guid? MusicBrainzReleaseId { get; init; }

    [JsonProperty(propertyName: "album")]
    public string? Album { get; init; }

    [JsonProperty(propertyName: "artist")]
    public string? Artist { get; init; }
}
