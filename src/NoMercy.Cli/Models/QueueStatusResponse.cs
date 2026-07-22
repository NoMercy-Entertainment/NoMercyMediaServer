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

internal class QueueStatusResponse
{
    [JsonProperty(propertyName: "workers")]
    public Dictionary<string, WorkerStatusResponse> Workers { get; set; } = new();

    [JsonProperty(propertyName: "pending_jobs")]
    public int PendingJobs { get; set; }

    [JsonProperty(propertyName: "failed_jobs")]
    public int FailedJobs { get; set; }
}

internal class WorkerStatusResponse
{
    [JsonProperty(propertyName: "active_threads")]
    public int ActiveThreads { get; set; }
}
