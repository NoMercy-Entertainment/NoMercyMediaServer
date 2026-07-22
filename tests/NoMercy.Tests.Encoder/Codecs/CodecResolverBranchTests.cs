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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// Branch-coverage gaps for <see cref="CodecResolver"/> beyond
/// <see cref="CodecResolverTests"/>:
///
/// • Copy codec short-circuits ALL preferences (Force/Prefer Software/Hardware
///   all return "copy" without consulting the codec definition matrix).
/// • PreferQuality is documented as identical to ForceSoftware.
/// • Unknown preference value falls through to PreferHardware.
/// • ResolveByEncoderName: known FFmpeg name → uses it directly.
/// • ResolveByEncoderName: unknown name → falls back to preference resolution.
/// • ResolveByEncoderName: vendor-encoder but no matching GPU → Device=null.
/// • ResolveByEncoderName: Copy codec → delegates to Resolve.
/// • ForceHardware throws with codec-name in message so the dashboard can
///   surface a useful error.
/// </summary>
public class CodecResolverBranchTests
{
    private readonly CodecRegistry _registry = new();

    private static IHardwareCapabilities MakeCaps(GpuVendor? vendor, VideoCodecType[] codecs)
    {
        List<GpuDevice> gpus = [];
        if (vendor.HasValue)
            gpus.Add(item: new(Vendor: vendor.Value, Name: "Test GPU", VramMb: 8192, MaxEncoderSessions: 12, SupportedCodecs: codecs));
        return new HardwareCapabilities(Gpus: gpus, CpuCores: 8);
    }

    // ── Copy short-circuit ────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: EncoderPreference.ForceSoftware)]
    [InlineData(data: EncoderPreference.ForceHardware)]
    [InlineData(data: EncoderPreference.PreferQuality)]
    [InlineData(data: EncoderPreference.PreferSpeed)]
    [InlineData(data: EncoderPreference.PreferHardware)]
    public void Copy_codec_returns_copy_encoder_regardless_of_preference(EncoderPreference pref)
    {
        // Stream copy bypasses the entire preference dispatch — pinned because
        // ForceHardware should NOT throw for Copy even with no GPU.
        IHardwareCapabilities noCaps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Copy, hardware: noCaps, preference: pref);

        resolved.FfmpegEncoderName.Should().Be(expected: "copy");
        resolved.Device.Should().BeNull();
    }

    // ── PreferQuality treated like ForceSoftware ─────────────────────────────

    [Fact]
    public void PreferQuality_with_GPU_present_still_picks_software()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.PreferQuality
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "libx264");
        resolved.Device.Should().BeNull();
    }

    // ── Unknown preference falls back to PreferHardware ──────────────────────

    [Fact]
    public void Unknown_preference_value_falls_back_to_prefer_hardware()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: (EncoderPreference)99 // bogus enum value
        );

        // PreferHardware path picks hardware when available.
        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
    }

    // ── ResolveByEncoderName paths ───────────────────────────────────────────

    [Fact]
    public void ResolveByEncoderName_known_software_handle_uses_it_directly()
    {
        // Pretend SpeedIndex picked libx264 — resolver must honor that choice
        // without consulting the GPU.
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H264,
            ffmpegEncoderName: "libx264",
            hardware: caps
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "libx264");
        resolved.Device.Should().BeNull(); // SW encoder has no GPU device
    }

    [Fact]
    public void ResolveByEncoderName_known_hardware_handle_attaches_matching_GPU()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H264,
            ffmpegEncoderName: "h264_nvenc",
            hardware: caps
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
        resolved.Device.Should().NotBeNull();
        resolved.Device!.Vendor.Should().Be(expected: GpuVendor.Nvidia);
    }

    [Fact]
    public void ResolveByEncoderName_hardware_handle_but_no_matching_GPU_returns_null_device()
    {
        // SpeedIndex says hevc_nvenc works on this host, but hardware probe
        // didn't enumerate a Nvidia GPU. The pipeline tolerates null Device for
        // hardware encoders — proves the resolver doesn't fail-fast.
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H265,
            ffmpegEncoderName: "hevc_nvenc",
            hardware: caps
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "hevc_nvenc");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void ResolveByEncoderName_unknown_name_falls_back_to_preference()
    {
        // Encoder name not in the codec definition table — fall back to
        // preference-based resolution so the pipeline still gets a valid
        // ResolvedCodec instead of crashing.
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H264,
            ffmpegEncoderName: "some_unknown_encoder",
            hardware: caps
        );

        // PreferHardware default → picks h264_nvenc since Nvidia is present.
        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
    }

    [Fact]
    public void ResolveByEncoderName_Copy_codec_delegates_to_Resolve()
    {
        IHardwareCapabilities noCaps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.Copy,
            ffmpegEncoderName: "ignored-encoder-name",
            hardware: noCaps
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "copy");
    }

    [Fact]
    public void ResolveByEncoderName_name_match_is_case_insensitive()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H264,
            ffmpegEncoderName: "H264_NVENC", // uppercase
            hardware: caps
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
    }

    // ── ForceHardware error surface ──────────────────────────────────────────

    [Fact]
    public void ForceHardware_no_GPU_throws_with_codec_name_in_message()
    {
        IHardwareCapabilities noCaps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        Action act = () =>
            resolver.Resolve(codec: VideoCodecType.H264, hardware: noCaps, preference: EncoderPreference.ForceHardware);

        act.Should()
            .Throw<InvalidOperationException>()
            .Which.Message.Should()
            .Contain(expected: "H264")
            .And.Contain(expected: "PreferHardware");
    }

    [Fact]
    public void ForceHardware_GPU_present_but_codec_unsupported_throws()
    {
        // Old AMD GPU that supports H.264 but not AV1 — ForceHardware on AV1
        // must throw, not silently fall back.
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        Action act = () =>
            resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps, preference: EncoderPreference.ForceHardware);

        act.Should().Throw<InvalidOperationException>();
    }
}
