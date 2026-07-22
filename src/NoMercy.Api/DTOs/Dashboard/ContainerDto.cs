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

namespace NoMercy.Api.DTOs.Dashboard;

public record ContainerDto
{
    [JsonProperty(propertyName: "label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty(propertyName: "value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "default")]
    public bool IsDefault { get; set; }

    [JsonProperty(propertyName: "available_video_codecs")]
    public VideoCodecDto[] AvailableVideoCodecs { get; set; } = [];

    [JsonProperty(propertyName: "available_audio_codecs")]
    public AudioCodecDto[] AvailableAudioCodecs { get; set; } = [];

    [JsonProperty(propertyName: "available_subtitle_codecs")]
    public SubtitleCodecDto[] AvailableSubtitleCodecs { get; set; } = [];

    [JsonProperty(propertyName: "available_resolutions")]
    public VideoQualityDto[] AvailableVideoSizes { get; set; } = [];
}

public class CodecDto
{
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty(propertyName: "simple_value")]
    public string SimpleValue { get; set; } = string.Empty;

    [JsonProperty(propertyName: "requires_gpu")]
    public bool RequiresGpu { get; set; }

    [JsonProperty(propertyName: "is_default")]
    public bool IsDefault { get; set; }
}

public class VideoQualityDto
{
    [JsonProperty(propertyName: "width")]
    public int Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public int Height { get; set; }

    [JsonProperty(propertyName: "label")]
    public string Label { get; set; } = string.Empty;
}

public class VideoCodecDto : CodecDto
{
    [JsonProperty(propertyName: "color_spaces")]
    public LabelValueDto[] AvailableVideoColorSpaces { get; set; } = [];

    [JsonProperty(propertyName: "tunes")]
    public LabelValueDto[] AvailableVideoTunes { get; set; } = [];

    [JsonProperty(propertyName: "profiles")]
    public LabelValueDto[] AvailableVideoProfiles { get; set; } = [];

    [JsonProperty(propertyName: "presets")]
    public LabelValueDto[] AvailablePresets { get; set; } = [];
}

public class AudioCodecDto : CodecDto
{
    [JsonProperty(propertyName: "available_languages")]
    public LabelValueDto[] AvailableLanguages { get; set; } = [];

    [JsonProperty(propertyName: "audio_quality_level")]
    public int AudioQualityLevel { get; set; }

    [JsonProperty(propertyName: "audio_channels")]
    public int AudioChannels { get; set; }

    [JsonProperty(propertyName: "hls_segment_filename")]
    public string HlsSegmentFilename { get; set; } = string.Empty;

    [JsonProperty(propertyName: "hls_playlist_filename")]
    public string HlsPlaylistFilename { get; set; } = string.Empty;

    [JsonProperty(propertyName: "bit_rate")]
    public long BitRate { get; set; }
}

public class SubtitleCodecDto : CodecDto
{
    [JsonProperty(propertyName: "available_languages")]
    public LabelValueDto[] AvailableLanguages { get; set; } = [];

    [JsonProperty(propertyName: "hls_segment_filename")]
    public string HlsSegmentFilename { get; set; } = string.Empty;

    [JsonProperty(propertyName: "hls_playlist_filename")]
    public string HlsPlaylistFilename { get; set; } = string.Empty;
}

public class LabelValueDto
{
    [JsonProperty(propertyName: "label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty(propertyName: "value")]
    public string Value { get; set; } = string.Empty;

    public LabelValueDto(string s)
    {
        Label = s;
        Value = s;
    }

    public LabelValueDto()
    {
        //
    }
}
