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

public record UserRequest
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty(propertyName: "manage")]
    public bool Manage { get; set; }

    [JsonProperty(propertyName: "owner")]
    public bool Owner { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "allowed")]
    public bool Allowed { get; set; }

    [JsonProperty(propertyName: "audio_transcoding")]
    public bool AudioTranscoding { get; set; }

    [JsonProperty(propertyName: "video_transcoding")]
    public bool VideoTranscoding { get; set; }

    [JsonProperty(propertyName: "no_transcoding")]
    public bool NoTranscoding { get; set; }

    [JsonProperty(propertyName: "libraries")]
    public Ulid[]? Libraries { get; set; }
}
