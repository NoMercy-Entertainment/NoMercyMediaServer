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

        error.Kind.Should().Be(expected: EncodingErrorKind.CodecUnavailable);
        error.Message.Should().Be(expected: "h264_nvenc not found");
        error.FfmpegStderr.Should().Be(expected: "Encoder not available");
        error.StageName.Should().Be(expected: "Validate");
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
    [InlineData(data: EncodingErrorKind.InputNotFound)]
    [InlineData(data: EncodingErrorKind.InputCorrupt)]
    [InlineData(data: EncodingErrorKind.InputUnsupported)]
    [InlineData(data: EncodingErrorKind.CodecUnavailable)]
    [InlineData(data: EncodingErrorKind.HardwareUnavailable)]
    [InlineData(data: EncodingErrorKind.HardwareFailure)]
    [InlineData(data: EncodingErrorKind.ProfileInvalid)]
    [InlineData(data: EncodingErrorKind.DiskFull)]
    [InlineData(data: EncodingErrorKind.Timeout)]
    [InlineData(data: EncodingErrorKind.Cancelled)]
    [InlineData(data: EncodingErrorKind.ProcessCrashed)]
    [InlineData(data: EncodingErrorKind.NetworkPathUnavailable)]
    [InlineData(data: EncodingErrorKind.NetworkPathTimeout)]
    [InlineData(data: EncodingErrorKind.NetworkPathPermission)]
    [InlineData(data: EncodingErrorKind.ResourceExhausted)]
    [InlineData(data: EncodingErrorKind.Unknown)]
    public void ErrorKind_AllValues_Exist(EncodingErrorKind kind)
    {
        kind.Should().BeDefined();
    }
}
