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
/// Chooses one candidate returned by <c>GET subtitles/search</c> and downloads it.
/// <see cref="DownloadUrl"/> is the opaque provider token from that search response's
/// <c>download_url</c> (equivalently <c>id</c>).
/// </summary>
public record SubtitleDownloadRequestDto(
    [property: JsonProperty("type")] string Type,
    [property: JsonProperty("id")] int Id,
    [property: JsonProperty("video_file_id")] string? VideoFileId,
    [property: JsonProperty("download_url")] string DownloadUrl,
    [property: JsonProperty("language")] string Language,
    [property: JsonProperty("format")] string? Format
);
