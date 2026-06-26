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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Encoder.Hardware;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard.Encoder;

[ApiController]
[Tags("Dashboard Hardware Benchmark")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/hardware/benchmark")]
public class HardwareBenchmarkController(IHardwareBenchmark benchmark) : BaseController
{
    [HttpGet]
    public IActionResult GetCachedIndex()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view hardware benchmarks");

        SpeedIndex index = benchmark.GetCachedIndex();
        BenchmarkProgress? progress = benchmark.CurrentProgress;
        return Ok(
            new
            {
                measurements = index.Measurements.Select(kvp => new
                {
                    codec = kvp.Key.Codec.ToString(),
                    encoder = kvp.Key.Encoder,
                    width = kvp.Key.Width,
                    device_name = kvp.Key.DeviceName,
                    fps = kvp.Value.Fps,
                    speed_multiplier = kvp.Value.SpeedMultiplier,
                    measured_at = kvp.Value.MeasuredAt,
                }),
                needs_recalibration = benchmark.NeedsRecalibration(),
                is_running = benchmark.IsRunning,
                progress = progress is null
                    ? null
                    : new
                    {
                        percent_complete = progress.PercentComplete,
                        completed_probes = progress.CompletedProbes,
                        total_probes = progress.TotalProbes,
                        stage = progress.Stage,
                        updated_at = progress.UpdatedAt,
                    },
            }
        );
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunBenchmark(CancellationToken ct)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse(
                "Only the server owner can trigger a hardware benchmark run"
            );

        SpeedIndex result = await benchmark.CalibrateAsync(ct);
        return Ok(new { measurements = result.Measurements.Count, message = "Benchmark complete" });
    }
}
