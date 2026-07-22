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

using Microsoft.AspNetCore.Mvc;
using Moq;
using NoMercy.Api.Controllers.V1.Encoder;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Startup;
using NoMercy.Resources;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Coverage for EncoderHardwareController: benchmark start/status/list, the
/// live utilization proxy, and the cached-capabilities probe endpoint. All
/// five collaborators are interfaces with no DB/network dependency, so the
/// controller is constructed directly with Moq doubles rather than through
/// NoMercyApiFactory.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class EncoderHardwareControllerTests
{
    private readonly Mock<IBenchmarkJobTracker> _tracker = new();
    private readonly Mock<IResourceMonitor> _monitor = new();
    private readonly Mock<IEncoderProcessRegistry> _registry = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly Mock<IFfmpegCapabilityProbe> _probe = new();

    private EncoderHardwareController CreateController() =>
        new(tracker: _tracker.Object, monitor: _monitor.Object, registry: _registry.Object, hardware: _hardware.Object, probe: _probe.Object);

    private static BenchmarkJobStatus MakeJob(string jobId, string status = "queued") =>
        new(JobId: jobId, Status: status, StartedAt: DateTime.UtcNow, CompletedAt: null, MeasurementCount: 0, RequestedCodecs: [], RequestedResolutions: [], Error: null);

    // =========================================================================
    // StartBenchmark
    // =========================================================================

    [Fact]
    public void StartBenchmark_NullRequest_StartsWithEmptyCodecsAndResolutions()
    {
        BenchmarkJobStatus job = MakeJob(jobId: "job-1");
        _tracker
            .Setup(expression: t =>
                t.Start(It.Is<List<VideoCodecType>>(c => c.Count == 0), It.IsAny<List<int>>())
            )
            .Returns(value: job);

        IActionResult result = CreateController().StartBenchmark(request: null);

        AcceptedResult accepted = Assert.IsType<AcceptedResult>(@object: result);
        _tracker.Verify(
            expression: t =>
                t.Start(
                    It.Is<List<VideoCodecType>>(c => c.Count == 0),
                    It.Is<List<int>>(r => r.Count == 0)
                ),
            times: Times.Once
        );
        accepted.Value.Should().NotBeNull();
    }

    [Fact]
    public void StartBenchmark_EmptyCodecsArray_StartsWithEmptyCodecs()
    {
        BenchmarkJobStatus job = MakeJob(jobId: "job-2");
        _tracker
            .Setup(expression: t => t.Start(It.IsAny<List<VideoCodecType>>(), It.IsAny<List<int>>()))
            .Returns(value: job);

        IActionResult result = CreateController()
            .StartBenchmark(request: new(Codecs: [], Resolutions: null));

        Assert.IsType<AcceptedResult>(@object: result);
        _tracker.Verify(
            expression: t => t.Start(It.Is<List<VideoCodecType>>(c => c.Count == 0), It.IsAny<List<int>>()),
            times: Times.Once
        );
    }

    [Fact]
    public void StartBenchmark_ValidCodecNames_ParsesAndStartsWithThoseCodecs()
    {
        BenchmarkJobStatus job = MakeJob(jobId: "job-3");
        List<VideoCodecType>? captured = null;
        _tracker
            .Setup(expression: t => t.Start(It.IsAny<List<VideoCodecType>>(), It.IsAny<List<int>>()))
            .Callback<IReadOnlyList<VideoCodecType>, IReadOnlyList<int>>(
                action: (codecs, _) => captured = codecs.ToList()
            )
            .Returns(value: job);

        IActionResult result = CreateController()
            .StartBenchmark(request: new(Codecs: ["H264", "Av1"], Resolutions: null));

        Assert.IsType<AcceptedResult>(@object: result);
        captured.Should().Equal(elements: [VideoCodecType.H264, VideoCodecType.Av1]);
    }

    [Fact]
    public void StartBenchmark_CaseInsensitiveCodecNames_StillParse()
    {
        BenchmarkJobStatus job = MakeJob(jobId: "job-4");
        List<VideoCodecType>? captured = null;
        _tracker
            .Setup(expression: t => t.Start(It.IsAny<List<VideoCodecType>>(), It.IsAny<List<int>>()))
            .Callback<IReadOnlyList<VideoCodecType>, IReadOnlyList<int>>(
                action: (codecs, _) => captured = codecs.ToList()
            )
            .Returns(value: job);

        IActionResult result = CreateController()
            .StartBenchmark(request: new(Codecs: ["h265"], Resolutions: null));

        Assert.IsType<AcceptedResult>(@object: result);
        captured.Should().Equal(elements: VideoCodecType.H265);
    }

    [Fact]
    public void StartBenchmark_WithResolutions_PassesResolutionsToTracker()
    {
        BenchmarkJobStatus job = MakeJob(jobId: "job-5");
        List<int>? captured = null;
        _tracker
            .Setup(expression: t => t.Start(It.IsAny<List<VideoCodecType>>(), It.IsAny<List<int>>()))
            .Callback<IReadOnlyList<VideoCodecType>, IReadOnlyList<int>>(
                action: (_, resolutions) => captured = resolutions.ToList()
            )
            .Returns(value: job);

        IActionResult result = CreateController()
            .StartBenchmark(request: new(Codecs: null, Resolutions: [1080, 2160]));

        Assert.IsType<AcceptedResult>(@object: result);
        captured.Should().Equal(elements: [1080, 2160]);
    }

    [Fact]
    public void StartBenchmark_UnknownCodecName_Returns422WithSuggestion()
    {
        IActionResult result = CreateController()
            .StartBenchmark(request: new(Codecs: ["not-a-real-codec"], Resolutions: null));

        UnprocessableEntityObjectResult unprocessable =
            Assert.IsType<UnprocessableEntityObjectResult>(@object: result);
        _tracker.Verify(
            expression: t => t.Start(It.IsAny<List<VideoCodecType>>(), It.IsAny<List<int>>()),
            times: Times.Never
        );
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(value: unprocessable.Value);
        json.Should().Contain(expected: "not-a-real-codec");
        json.Should().Contain(expected: "H264");
    }

    [Fact]
    public void StartBenchmark_MultipleCodecs_FirstInvalidNameWins_NeverReachesSecondEntry()
    {
        // Two invalid entries — only the FIRST must be reported and the loop
        // must stop there rather than continuing to validate the rest.
        IActionResult result = CreateController()
            .StartBenchmark(request: new(Codecs: ["totally-bogus", "also-bogus"], Resolutions: null));

        UnprocessableEntityObjectResult unprocessable =
            Assert.IsType<UnprocessableEntityObjectResult>(@object: result);
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(value: unprocessable.Value);
        json.Should().Contain(expected: "totally-bogus");
        json.Should().NotContain(unexpected: "also-bogus");
    }

    [Fact]
    public void StartBenchmark_ValidThenInvalidCodec_Returns422_NeverStartsJob()
    {
        IActionResult result = CreateController()
            .StartBenchmark(request: new(Codecs: ["H264", "not-a-real-codec"], Resolutions: null));

        Assert.IsType<UnprocessableEntityObjectResult>(@object: result);
        _tracker.Verify(
            expression: t => t.Start(It.IsAny<List<VideoCodecType>>(), It.IsAny<List<int>>()),
            times: Times.Never
        );
    }

    // =========================================================================
    // GetBenchmark
    // =========================================================================

    [Fact]
    public void GetBenchmark_UnknownJobId_Returns404()
    {
        _tracker.Setup(expression: t => t.Get("missing-job")).Returns(value: (BenchmarkJobStatus?)null);

        IActionResult result = CreateController().GetBenchmark(jobId: "missing-job");

        ObjectResult objectResult = Assert.IsAssignableFrom<ObjectResult>(@object: result);
        objectResult.StatusCode.Should().Be(expected: 404);
    }

    [Fact]
    public void GetBenchmark_KnownJobId_ReturnsJobStatus()
    {
        BenchmarkJobStatus job = MakeJob(jobId: "job-known", status: "completed");
        _tracker.Setup(expression: t => t.Get("job-known")).Returns(value: job);

        IActionResult result = CreateController().GetBenchmark(jobId: "job-known");

        OkObjectResult ok = Assert.IsType<OkObjectResult>(@object: result);
        ok.Value.Should().Be(expected: job);
    }

    // =========================================================================
    // ListBenchmarks
    // =========================================================================

    [Fact]
    public void ListBenchmarks_ReturnsAllTrackedJobs()
    {
        List<BenchmarkJobStatus> jobs = [MakeJob(jobId: "a"), MakeJob(jobId: "b", status: "completed")];
        _tracker.Setup(expression: t => t.List()).Returns(value: jobs);

        IActionResult result = CreateController().ListBenchmarks();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(@object: result);
        ok.Value.Should().NotBeNull();
    }

    // =========================================================================
    // GetUtilization — proxies all four dependencies into one snapshot
    // =========================================================================

    [Fact]
    public async Task GetUtilization_CombinesAllFourCollaboratorsIntoOneSnapshot()
    {
        _monitor.Setup(expression: m => m.GetCpuUsagePercent()).Returns(value: 42.5);
        _monitor.Setup(expression: m => m.GetAvailableMemoryMb()).Returns(value: 8192L);
        List<GpuProcessSample> gpuSamples = [new(Pid: 1234, GpuIndex: 0, EncoderUtilizationPercent: 55, EncoderMemoryBytes: 512_000_000)];
        _monitor
            .Setup(expression: m => m.SampleGpuAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: gpuSamples);
        _registry.Setup(expression: r => r.CountConcurrentNvencSessions()).Returns(value: 2);
        List<GpuDevice> gpus =
        [
            new(Vendor: GpuVendor.Nvidia, Name: "RTX 4070", VramMb: 12_000, MaxEncoderSessions: 3, SupportedCodecs: [VideoCodecType.H264]),
        ];
        _hardware.Setup(expression: h => h.Gpus).Returns(value: gpus);

        IActionResult result = await CreateController().GetUtilization();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(@object: result);
        UtilizationSnapshot snapshot = Assert.IsType<UtilizationSnapshot>(@object: ok.Value);
        snapshot.CpuUsagePercent.Should().Be(expected: 42.5);
        snapshot.AvailableMemoryMb.Should().Be(expected: 8192L);
        snapshot.GpuSamples.Should().BeEquivalentTo(expectation: gpuSamples);
        snapshot.ConcurrentNvencSessions.Should().Be(expected: 2);
        snapshot.Gpus.Should().BeEquivalentTo(expectation: gpus);
    }

    // =========================================================================
    // GetCapabilities
    // =========================================================================

    [Fact]
    public void GetCapabilities_ProbeNotYetCompleted_ReturnsProbePendingStatus()
    {
        _probe.Setup(expression: p => p.GetCachedReport()).Returns(value: (CapabilityReport?)null);

        IActionResult result = CreateController().GetCapabilities();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(@object: result);
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(value: ok.Value);
        json.Should().Contain(expected: "probe_pending");
    }

    [Fact]
    public void GetCapabilities_CachedReportAvailable_ReturnsTheReport()
    {
        CapabilityReport report = new(
            BluRayProtocol: true,
            DvdReadProtocol: false,
            AvailableEncoders: ["libx264", "h264_nvenc"],
            MissingFilters: [],
            MissingMuxers: [],
            FpcalcPresent: true,
            WhisperModelPresent: false,
            TesseractEngTraineddataPresent: true,
            TesseractModelsDirectory: "/models",
            Issues: []
        );
        _probe.Setup(expression: p => p.GetCachedReport()).Returns(value: report);

        IActionResult result = CreateController().GetCapabilities();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(@object: result);
        ok.Value.Should().Be(expected: report);
    }
}
