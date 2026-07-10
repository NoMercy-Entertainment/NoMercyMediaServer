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
/// A single subtitle search hit, mapped from an <c>Encoder.Subtitles.SubtitleCandidate</c>.
/// <see cref="Id"/> and <see cref="DownloadUrl"/> are the same opaque provider URL — the
/// future download endpoint accepts either as the token to fetch this exact subtitle.
/// </summary>
public record SubtitleSearchResultDto(
    [property: JsonProperty("id")] string Id,
    [property: JsonProperty("download_url")] string DownloadUrl,
    [property: JsonProperty("language")] string Language,
    [property: JsonProperty("language_name")] string LanguageName,
    [property: JsonProperty("release_name")] string? ReleaseName,
    [property: JsonProperty("file_name")] string? FileName,
    [property: JsonProperty("downloads")] int Downloads,
    [property: JsonProperty("rating")] double Rating,
    [property: JsonProperty("format")] string Format,
    [property: JsonProperty("hearing_impaired")] bool HearingImpaired,
    [property: JsonProperty("fps")] double? Fps,
    [property: JsonProperty("uploader")] string? Uploader,
    [property: JsonProperty("trusted")] bool Trusted
);
