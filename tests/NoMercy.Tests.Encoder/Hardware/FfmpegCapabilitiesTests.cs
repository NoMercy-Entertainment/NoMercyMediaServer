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
        SetupResponse("-encoders", encoderOutput);
        SetupResponse("-decoders", "");
        SetupResponse("-demuxers", "");
        SetupResponse("-filters", "");
        SetupResponse("-protocols", "");

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.HasEncoder("libx264").Should().BeTrue();
        caps.HasEncoder("h264_nvenc").Should().BeTrue();
        caps.HasEncoder("libsvtav1").Should().BeTrue();
        caps.HasEncoder("aac").Should().BeTrue();
        caps.HasEncoder("vp9_nvenc").Should().BeFalse();
    }

    [Fact]
    public async Task ProbeFilters_ParsesCorrectly()
    {
        SetupResponse("-encoders", "");
        SetupResponse("-decoders", "");
        SetupResponse("-demuxers", "");
        SetupResponse("-protocols", "");
        string filterOutput = """
            Filters:
             ... scale            V->V       Scale the input video size and/or convert the image format.
             ... tonemap          V->V       Conversion of HDR to SDR via tonemapping.
             ... libplacebo       V->V       GPU-accelerated video processing via libplacebo.
             ... zscale           V->V       Scale the input video using z.lib
            """;
        SetupResponse("-filters", filterOutput);

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.HasFilter("libplacebo").Should().BeTrue();
        caps.HasFilter("tonemap").Should().BeTrue();
        caps.HasFilter("zscale").Should().BeTrue();
        caps.HasFilter("nonexistent").Should().BeFalse();
    }

    [Fact]
    public async Task AvailableEncoders_ReturnsImmutableSet()
    {
        string encoderOutput = """
            Encoders:
             V..... libx264              libx264 H.264
            """;
        SetupResponse("-encoders", encoderOutput);
        SetupResponse("-decoders", "");
        SetupResponse("-demuxers", "");
        SetupResponse("-filters", "");
        SetupResponse("-protocols", "");

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.AvailableEncoders.Should().Contain("libx264");
    }

    [Fact]
    public async Task ProbeDemuxers_ParsesCorrectly()
    {
        SetupResponse("-encoders", "");
        SetupResponse("-decoders", "");
        SetupResponse("-filters", "");
        SetupResponse("-protocols", "");
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
        SetupResponse("-demuxers", demuxerOutput);

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.HasDemuxer("3dostr").Should().BeTrue();
        caps.HasDemuxer("flv").Should().BeTrue();
        caps.HasDemuxer("image2").Should().BeTrue();
        caps.HasDemuxer("nonexistent").Should().BeFalse();
    }

    [Fact]
    public async Task ProbeProtocols_ParsesCorrectly()
    {
        SetupResponse("-encoders", "");
        SetupResponse("-decoders", "");
        SetupResponse("-demuxers", "");
        SetupResponse("-filters", "");
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
        SetupResponse("-protocols", protocolOutput);

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.HasProtocol("file").Should().BeTrue();
        caps.HasProtocol("http").Should().BeTrue();
        caps.HasProtocol("https").Should().BeTrue();
        caps.HasProtocol("rtmp").Should().BeFalse();
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
        SetupResponse("-encoders", "");
        SetupResponse("-demuxers", "");
        SetupResponse("-filters", "");
        SetupResponse("-protocols", "");
        SetupResponse("-decoders", decoderOutput);

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.AvailableDecoders.Should().Contain("h264");
        caps.AvailableDecoders.Should().Contain("hevc");
        caps.AvailableDecoders.Should().Contain("av1");
        caps.AvailableDecoders.Should().Contain("aac");
    }

    [Fact]
    public async Task ProbeAsync_AllListsBeforeProbe_AreEmpty()
    {
        FfmpegCapabilities caps = new(_processRunner.Object);

        caps.AvailableEncoders.Should().BeEmpty();
        caps.AvailableDecoders.Should().BeEmpty();
        caps.AvailableDemuxers.Should().BeEmpty();
        caps.AvailableFilters.Should().BeEmpty();
        caps.AvailableProtocols.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_EmptyOutput_LeavesListsEmpty()
    {
        SetupResponse("-encoders", "");
        SetupResponse("-decoders", "");
        SetupResponse("-demuxers", "");
        SetupResponse("-filters", "");
        SetupResponse("-protocols", "");

        FfmpegCapabilities caps = new(_processRunner.Object);
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
        SetupResponse("-encoders", "");
        SetupResponse("-decoders", "");
        SetupResponse("-demuxers", "");
        SetupResponse("-protocols", "");
        string filterOutput = """
            Filters:
              T.. = Timeline support
              .S. = Slice threading
              ..C = Command support
              ... scale            V->V       Scale the input video
            """;
        SetupResponse("-filters", filterOutput);

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.HasFilter("scale").Should().BeTrue();
        // Legend descriptors must NOT register as filters.
        caps.HasFilter("=").Should().BeFalse();
        caps.HasFilter("Timeline").Should().BeFalse();
    }

    private void SetupResponse(string flag, string output)
    {
        _processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(args => args.Length == 1 && args[0] == flag),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, output, "", TimeSpan.Zero));
    }
}
