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

using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// Pins <see cref="GpuEncoderTokens.VendorForEncoderName"/> — the map that
/// gates hardware-encoder selection on a physically detected GPU (see
/// <c>PlanStage.IsHardwareEncoderPhysicallyAvailable</c>). A wrong or missing
/// mapping here silently reopens the field bug where "h264_amf" got treated
/// as selectable on an NVIDIA-only host.
/// </summary>
public class GpuEncoderTokensVendorMappingTests
{
    [Theory]
    [InlineData(data: ["h264_nvenc", GpuVendor.Nvidia])]
    [InlineData(data: ["hevc_nvenc", GpuVendor.Nvidia])]
    [InlineData(data: ["av1_nvenc", GpuVendor.Nvidia])]
    [InlineData(data: ["h264_amf", GpuVendor.Amd])]
    [InlineData(data: ["hevc_amf", GpuVendor.Amd])]
    [InlineData(data: ["av1_amf", GpuVendor.Amd])]
    [InlineData(data: ["h264_qsv", GpuVendor.Intel])]
    [InlineData(data: ["hevc_qsv", GpuVendor.Intel])]
    [InlineData(data: ["vp9_qsv", GpuVendor.Intel])]
    [InlineData(data: ["h264_vaapi", GpuVendor.Intel])]
    [InlineData(data: ["vp9_vaapi", GpuVendor.Intel])]
    [InlineData(data: ["h264_videotoolbox", GpuVendor.Apple])]
    [InlineData(data: ["hevc_videotoolbox", GpuVendor.Apple])]
    public void VendorForEncoderName_MapsKnownHardwareEncoders(
        string encoderName,
        GpuVendor expectedVendor
    )
    {
        GpuEncoderTokens.VendorForEncoderName(ffmpegEncoderName: encoderName).Should().Be(expected: expectedVendor);
    }

    [Theory]
    [InlineData(data: "H264_AMF")]
    [InlineData(data: "H264_NVENC")]
    public void VendorForEncoderName_IsCaseInsensitive(string encoderName)
    {
        GpuEncoderTokens.VendorForEncoderName(ffmpegEncoderName: encoderName).Should().NotBeNull();
    }

    [Theory]
    [InlineData(data: "libx264")]
    [InlineData(data: "libx265")]
    [InlineData(data: "libsvtav1")]
    [InlineData(data: "libvpx-vp9")]
    [InlineData(data: "copy")]
    [InlineData(data: "some_future_encoder")]
    public void VendorForEncoderName_ReturnsNullForSoftwareOrUnknownEncoders(string encoderName)
    {
        GpuEncoderTokens.VendorForEncoderName(ffmpegEncoderName: encoderName).Should().BeNull();
    }
}
