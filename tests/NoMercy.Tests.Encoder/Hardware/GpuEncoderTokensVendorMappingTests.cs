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
    [InlineData(["h264_nvenc", GpuVendor.Nvidia])]
    [InlineData(["hevc_nvenc", GpuVendor.Nvidia])]
    [InlineData(["av1_nvenc", GpuVendor.Nvidia])]
    [InlineData(["h264_amf", GpuVendor.Amd])]
    [InlineData(["hevc_amf", GpuVendor.Amd])]
    [InlineData(["av1_amf", GpuVendor.Amd])]
    [InlineData(["h264_qsv", GpuVendor.Intel])]
    [InlineData(["hevc_qsv", GpuVendor.Intel])]
    [InlineData(["vp9_qsv", GpuVendor.Intel])]
    [InlineData(["h264_vaapi", GpuVendor.Intel])]
    [InlineData(["vp9_vaapi", GpuVendor.Intel])]
    [InlineData(["h264_videotoolbox", GpuVendor.Apple])]
    [InlineData(["hevc_videotoolbox", GpuVendor.Apple])]
    public void VendorForEncoderName_MapsKnownHardwareEncoders(
        string encoderName,
        GpuVendor expectedVendor
    )
    {
        GpuEncoderTokens.VendorForEncoderName(encoderName).Should().Be(expectedVendor);
    }

    [Theory]
    [InlineData("H264_AMF")]
    [InlineData("H264_NVENC")]
    public void VendorForEncoderName_IsCaseInsensitive(string encoderName)
    {
        GpuEncoderTokens.VendorForEncoderName(encoderName).Should().NotBeNull();
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    [InlineData("libvpx-vp9")]
    [InlineData("copy")]
    [InlineData("some_future_encoder")]
    public void VendorForEncoderName_ReturnsNullForSoftwareOrUnknownEncoders(string encoderName)
    {
        GpuEncoderTokens.VendorForEncoderName(encoderName).Should().BeNull();
    }
}
