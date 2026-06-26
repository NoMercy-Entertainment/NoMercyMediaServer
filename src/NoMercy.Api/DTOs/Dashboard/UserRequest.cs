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
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("manage")]
    public bool Manage { get; set; }

    [JsonProperty("owner")]
    public bool Owner { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("allowed")]
    public bool Allowed { get; set; }

    [JsonProperty("audio_transcoding")]
    public bool AudioTranscoding { get; set; }

    [JsonProperty("video_transcoding")]
    public bool VideoTranscoding { get; set; }

    [JsonProperty("no_transcoding")]
    public bool NoTranscoding { get; set; }

    [JsonProperty("libraries")]
    public Ulid[]? Libraries { get; set; }
}
