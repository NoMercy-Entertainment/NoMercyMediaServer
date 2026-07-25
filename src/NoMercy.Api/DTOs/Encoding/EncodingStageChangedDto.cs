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

namespace NoMercy.Api.DTOs.Encoding;

public record EncodingStageChangedDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("base_folder")]
    public string BaseFolder { get; set; } = string.Empty;

    [JsonProperty("share_path")]
    public string SharePath { get; set; } = string.Empty;

    [JsonProperty("video_streams")]
    public List<string> VideoStreams { get; set; } = [];

    [JsonProperty("audio_streams")]
    public List<string> AudioStreams { get; set; } = [];

    [JsonProperty("subtitle_streams")]
    public List<string> SubtitleStreams { get; set; } = [];

    [JsonProperty("has_gpu")]
    public bool HasGpu { get; set; }

    [JsonProperty("is_hdr")]
    public bool IsHdr { get; set; }
}
