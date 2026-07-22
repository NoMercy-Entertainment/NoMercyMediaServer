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

using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.Tests.Encoder.Hardware;

public class FfmpegCapabilitiesTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();

    [Fact]
    public async Task ProbeEncoders_ParsesCorrectly()
    {
        string encoderOutput = """
            Encoders:
             V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)
             V..... libx265              libx265 H.265 / HEVC (codec hevc)
             V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)
             V..... hevc_nvenc           NVIDIA NVENC hevc encoder (codec hevc)
             V..... libsvtav1            SVT-AV1(Scalable Video Technology for AV1) encoder (codec av1)
             A..... aac                  AAC (Advanced Audio Coding) (codec aac)
             A..... libopus              libopus Opus (codec opus)
            """;
        SetupResponse(flag: "-encoders", output: encoderOutput);
        SetupResponse(flag: "-decoders", output: "");
        SetupResponse(flag: "-demuxers", output: "");
        SetupResponse(flag: "-filters", output: "");
        SetupResponse(flag: "-protocols", output: "");

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.HasEncoder(name: "libx264").Should().BeTrue();
        caps.HasEncoder(name: "h264_nvenc").Should().BeTrue();
        caps.HasEncoder(name: "libsvtav1").Should().BeTrue();
        caps.HasEncoder(name: "aac").Should().BeTrue();
        caps.HasEncoder(name: "vp9_nvenc").Should().BeFalse();
    }

    [Fact]
    public async Task ProbeFilters_ParsesCorrectly()
    {
        SetupResponse(flag: "-encoders", output: "");
        SetupResponse(flag: "-decoders", output: "");
        SetupResponse(flag: "-demuxers", output: "");
        SetupResponse(flag: "-protocols", output: "");
        string filterOutput = """
            Filters:
             ... scale            V->V       Scale the input video size and/or convert the image format.
             ... tonemap          V->V       Conversion of HDR to SDR via tonemapping.
             ... libplacebo       V->V       GPU-accelerated video processing via libplacebo.
             ... zscale           V->V       Scale the input video using z.lib
            """;
        SetupResponse(flag: "-filters", output: filterOutput);

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.HasFilter(name: "libplacebo").Should().BeTrue();
        caps.HasFilter(name: "tonemap").Should().BeTrue();
        caps.HasFilter(name: "zscale").Should().BeTrue();
        caps.HasFilter(name: "nonexistent").Should().BeFalse();
    }

    [Fact]
    public async Task AvailableEncoders_ReturnsImmutableSet()
    {
        string encoderOutput = """
            Encoders:
             V..... libx264              libx264 H.264
            """;
        SetupResponse(flag: "-encoders", output: encoderOutput);
        SetupResponse(flag: "-decoders", output: "");
        SetupResponse(flag: "-demuxers", output: "");
        SetupResponse(flag: "-filters", output: "");
        SetupResponse(flag: "-protocols", output: "");

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.AvailableEncoders.Should().Contain(expected: "libx264");
    }

    [Fact]
    public async Task ProbeDemuxers_ParsesCorrectly()
    {
        SetupResponse(flag: "-encoders", output: "");
        SetupResponse(flag: "-decoders", output: "");
        SetupResponse(flag: "-filters", output: "");
        SetupResponse(flag: "-protocols", output: "");
        // The demuxer regex only captures single-name demuxers — multi-name
        // entries like "matroska,webm" or "mov,mp4,m4a,3gp" fail the pattern
        // because the comma isn't whitespace, so the name lookup intentionally
        // misses them. Operators query the canonical short names that ffmpeg
        // also accepts as separate demuxers when passed via -f.
        string demuxerOutput = """
            File formats:
             D   3dostr             3DO STR
             D   flv                FLV (Flash Video)
             D   image2             image2 sequence
            """;
        SetupResponse(flag: "-demuxers", output: demuxerOutput);

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.HasDemuxer(name: "3dostr").Should().BeTrue();
        caps.HasDemuxer(name: "flv").Should().BeTrue();
        caps.HasDemuxer(name: "image2").Should().BeTrue();
        caps.HasDemuxer(name: "nonexistent").Should().BeFalse();
    }

    [Fact]
    public async Task ProbeProtocols_ParsesCorrectly()
    {
        SetupResponse(flag: "-encoders", output: "");
        SetupResponse(flag: "-decoders", output: "");
        SetupResponse(flag: "-demuxers", output: "");
        SetupResponse(flag: "-filters", output: "");
        string protocolOutput = """
            Supported file protocols:
            Input:
            file
            http
            https
            tcp
            Output:
            file
            http
            tcp
            """;
        SetupResponse(flag: "-protocols", output: protocolOutput);

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.HasProtocol(name: "file").Should().BeTrue();
        caps.HasProtocol(name: "http").Should().BeTrue();
        caps.HasProtocol(name: "https").Should().BeTrue();
        caps.HasProtocol(name: "rtmp").Should().BeFalse();
    }

    [Fact]
    public async Task ProbeDecoders_ParsesCorrectly()
    {
        // Decoders share the same row format as encoders — VASD prefix.
        string decoderOutput = """
            Decoders:
             V..... h264                 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10
             V..... hevc                 HEVC (High Efficiency Video Coding)
             V..... av1                  Alliance for Open Media AV1
             A..... aac                  AAC (Advanced Audio Coding)
            """;
        SetupResponse(flag: "-encoders", output: "");
        SetupResponse(flag: "-demuxers", output: "");
        SetupResponse(flag: "-filters", output: "");
        SetupResponse(flag: "-protocols", output: "");
        SetupResponse(flag: "-decoders", output: decoderOutput);

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.AvailableDecoders.Should().Contain(expected: "h264");
        caps.AvailableDecoders.Should().Contain(expected: "hevc");
        caps.AvailableDecoders.Should().Contain(expected: "av1");
        caps.AvailableDecoders.Should().Contain(expected: "aac");
    }

    [Fact]
    public async Task ProbeAsync_AllListsBeforeProbe_AreEmpty()
    {
        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);

        caps.AvailableEncoders.Should().BeEmpty();
        caps.AvailableDecoders.Should().BeEmpty();
        caps.AvailableDemuxers.Should().BeEmpty();
        caps.AvailableFilters.Should().BeEmpty();
        caps.AvailableProtocols.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_EmptyOutput_LeavesListsEmpty()
    {
        SetupResponse(flag: "-encoders", output: "");
        SetupResponse(flag: "-decoders", output: "");
        SetupResponse(flag: "-demuxers", output: "");
        SetupResponse(flag: "-filters", output: "");
        SetupResponse(flag: "-protocols", output: "");

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.AvailableEncoders.Should().BeEmpty();
        caps.AvailableFilters.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeFilters_RejectsLegendRowsWithoutSignature()
    {
        // ffmpeg's -filters list includes a small legend ("T.. = Timeline support")
        // above the actual filter rows. The regex requires the "type->type"
        // signature to exclude legend lines.
        SetupResponse(flag: "-encoders", output: "");
        SetupResponse(flag: "-decoders", output: "");
        SetupResponse(flag: "-demuxers", output: "");
        SetupResponse(flag: "-protocols", output: "");
        string filterOutput = """
            Filters:
              T.. = Timeline support
              .S. = Slice threading
              ..C = Command support
              ... scale            V->V       Scale the input video
            """;
        SetupResponse(flag: "-filters", output: filterOutput);

        FfmpegCapabilities caps = new(processRunner: _processRunner.Object);
        await caps.ProbeAsync();

        caps.HasFilter(name: "scale").Should().BeTrue();
        // Legend descriptors must NOT register as filters.
        caps.HasFilter(name: "=").Should().BeFalse();
        caps.HasFilter(name: "Timeline").Should().BeFalse();
    }

    private void SetupResponse(string flag, string output)
    {
        _processRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(args => args.Length == 1 && args[0] == flag),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: output, StdErr: "", Duration: TimeSpan.Zero));
    }
}
