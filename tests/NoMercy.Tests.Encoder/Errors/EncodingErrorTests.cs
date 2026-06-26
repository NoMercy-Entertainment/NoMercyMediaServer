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

using NoMercy.Encoder.Errors;

namespace NoMercy.Tests.Encoder.Errors;

public class EncodingErrorTests
{
    [Fact]
    public void Error_WithAllFields_RoundTrips()
    {
        EncodingError error = new(
            Kind: EncodingErrorKind.CodecUnavailable,
            Message: "h264_nvenc not found",
            FfmpegStderr: "Encoder not available",
            StageName: "Validate",
            Recoverable: false
        );

        error.Kind.Should().Be(EncodingErrorKind.CodecUnavailable);
        error.Message.Should().Be("h264_nvenc not found");
        error.FfmpegStderr.Should().Be("Encoder not available");
        error.StageName.Should().Be("Validate");
        error.Recoverable.Should().BeFalse();
    }

    [Fact]
    public void Error_WithNullOptionals_Allowed()
    {
        EncodingError error = new(
            Kind: EncodingErrorKind.Cancelled,
            Message: "User cancelled",
            FfmpegStderr: null,
            StageName: null,
            Recoverable: false
        );

        error.FfmpegStderr.Should().BeNull();
        error.StageName.Should().BeNull();
    }

    [Theory]
    [InlineData(EncodingErrorKind.InputNotFound)]
    [InlineData(EncodingErrorKind.InputCorrupt)]
    [InlineData(EncodingErrorKind.InputUnsupported)]
    [InlineData(EncodingErrorKind.CodecUnavailable)]
    [InlineData(EncodingErrorKind.HardwareUnavailable)]
    [InlineData(EncodingErrorKind.HardwareFailure)]
    [InlineData(EncodingErrorKind.ProfileInvalid)]
    [InlineData(EncodingErrorKind.DiskFull)]
    [InlineData(EncodingErrorKind.Timeout)]
    [InlineData(EncodingErrorKind.Cancelled)]
    [InlineData(EncodingErrorKind.ProcessCrashed)]
    [InlineData(EncodingErrorKind.NetworkPathUnavailable)]
    [InlineData(EncodingErrorKind.NetworkPathTimeout)]
    [InlineData(EncodingErrorKind.NetworkPathPermission)]
    [InlineData(EncodingErrorKind.ResourceExhausted)]
    [InlineData(EncodingErrorKind.Unknown)]
    public void ErrorKind_AllValues_Exist(EncodingErrorKind kind)
    {
        kind.Should().BeDefined();
    }
}
