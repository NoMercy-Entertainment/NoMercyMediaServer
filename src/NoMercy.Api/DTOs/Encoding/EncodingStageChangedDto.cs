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
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty(propertyName: "base_folder")]
    public string BaseFolder { get; set; } = string.Empty;

    [JsonProperty(propertyName: "share_path")]
    public string SharePath { get; set; } = string.Empty;

    [JsonProperty(propertyName: "video_streams")]
    public List<string> VideoStreams { get; set; } = [];

    [JsonProperty(propertyName: "audio_streams")]
    public List<string> AudioStreams { get; set; } = [];

    [JsonProperty(propertyName: "subtitle_streams")]
    public List<string> SubtitleStreams { get; set; } = [];

    [JsonProperty(propertyName: "has_gpu")]
    public bool HasGpu { get; set; }

    [JsonProperty(propertyName: "is_hdr")]
    public bool IsHdr { get; set; }
}
