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

public class ConfigDto
{
    [JsonProperty(propertyName: "data")]
    public ConfigDtoData Data { get; set; } = new();
}

public class ConfigDtoData
{
    [JsonProperty(propertyName: "internal_port")]
    public int InternalServerPort { get; set; }

    [JsonProperty(propertyName: "external_port")]
    public int ExternalServerPort { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? ServerName { get; set; }

    [JsonProperty(propertyName: "library_workers")]
    public int? LibraryWorkers { get; set; }

    [JsonProperty(propertyName: "import_workers")]
    public int? ImportWorkers { get; set; }

    [JsonProperty(propertyName: "extras_workers")]
    public int? ExtrasWorkers { get; set; }

    [JsonProperty(propertyName: "encoder_workers")]
    public int? EncoderWorkers { get; set; }

    [JsonProperty(propertyName: "cron_workers")]
    public int? CronWorkers { get; set; }

    [JsonProperty(propertyName: "image_workers")]
    public int? ImageWorkers { get; set; }

    [JsonProperty(propertyName: "file_workers")]
    public int? FileWorkers { get; set; }

    [JsonProperty(propertyName: "music_workers")]
    public int? MusicWorkers { get; set; }

    [JsonProperty(propertyName: "swagger")]
    public bool? Swagger { get; set; }

    [JsonProperty(propertyName: "allow_adult_content")]
    public bool? AllowAdultContent { get; set; }

    [JsonProperty(propertyName: "use_synthesized_dns")]
    public bool? UseSynthesizedDns { get; set; }
}
