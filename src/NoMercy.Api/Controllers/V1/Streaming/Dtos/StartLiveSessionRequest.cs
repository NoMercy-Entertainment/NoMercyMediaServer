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
using NoMercy.Encoder.Codecs;

namespace NoMercy.Api.Controllers.V1.Streaming.Dtos;

public record StartLiveSessionRequest(
    [property: JsonProperty(propertyName: "video_file_id")] string VideoFileId,
    [property: JsonProperty(propertyName: "client_caps")] ClientCapabilitiesDto ClientCaps,
    [property: JsonProperty(propertyName: "start_time_seconds")] double StartTimeSeconds,
    [property: JsonProperty(propertyName: "preferred_quality")] string? PreferredQuality,
    // ISO 639 language of the audio track the viewer picked from the episode's
    // own language list. The encoder maps the matching source stream. Null = let
    // the server default (English, then the file's default track).
    [property: JsonProperty(propertyName: "audio_language")] string? AudioLanguage = null
);

public record ClientCapabilitiesDto(
    [property: JsonProperty(propertyName: "video_codecs")] VideoCodecType[] VideoCodecs,
    [property: JsonProperty(propertyName: "audio_codecs")] AudioCodecType[] AudioCodecs,
    [property: JsonProperty(propertyName: "containers")] string[] Containers,
    [property: JsonProperty(propertyName: "max_width")] int MaxWidth,
    [property: JsonProperty(propertyName: "max_height")] int MaxHeight,
    [property: JsonProperty(propertyName: "supports_hdr")] bool SupportsHdr,
    [property: JsonProperty(propertyName: "supports_10bit")] bool Supports10Bit,
    [property: JsonProperty(propertyName: "max_bitrate_kbps")] int MaxBitrateKbps,
    [property: JsonProperty(propertyName: "max_audio_channels")] int MaxAudioChannels = 2
);
