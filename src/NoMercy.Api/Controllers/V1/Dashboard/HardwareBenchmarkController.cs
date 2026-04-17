using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Encoder.Hardware;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

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
