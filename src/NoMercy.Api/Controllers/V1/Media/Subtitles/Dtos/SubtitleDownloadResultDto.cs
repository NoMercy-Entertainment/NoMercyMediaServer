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

namespace NoMercy.Api.Controllers.V1.Media.Subtitles.Dtos;

/// <summary>
/// The newly downloaded + persisted subtitle sidecar, shaped like the track
/// entries <c>VideoPlaylistResponseDto.Tracks</c> emits for subtitles, so the
/// client can push it straight into the current playback session without
/// waiting for the next watch response.
/// </summary>
public record SubtitleDownloadResultDto(
    [property: JsonProperty(propertyName: "file")] string File,
    [property: JsonProperty(propertyName: "kind")] string Kind,
    [property: JsonProperty(propertyName: "label")] string Label,
    [property: JsonProperty(propertyName: "language")] string Language
);
