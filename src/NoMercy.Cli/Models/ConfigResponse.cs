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

namespace NoMercy.Cli.Models;

internal class ConfigResponse
{
    [JsonProperty(propertyName: "internal_port")]
    public int InternalPort { get; set; }

    [JsonProperty(propertyName: "external_port")]
    public int ExternalPort { get; set; }

    [JsonProperty(propertyName: "server_name")]
    public string? ServerName { get; set; }

    [JsonProperty(propertyName: "queue_workers")]
    public int QueueWorkers { get; set; }

    [JsonProperty(propertyName: "encoder_workers")]
    public int EncoderWorkers { get; set; }

    [JsonProperty(propertyName: "cron_workers")]
    public int CronWorkers { get; set; }

    [JsonProperty(propertyName: "data_workers")]
    public int DataWorkers { get; set; }

    [JsonProperty(propertyName: "image_workers")]
    public int ImageWorkers { get; set; }

    [JsonProperty(propertyName: "file_workers")]
    public int FileWorkers { get; set; }

    [JsonProperty(propertyName: "request_workers")]
    public int RequestWorkers { get; set; }

    [JsonProperty(propertyName: "swagger")]
    public bool Swagger { get; set; }
}
