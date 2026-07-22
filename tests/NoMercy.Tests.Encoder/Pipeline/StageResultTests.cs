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
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Pipeline;

public class StageResultTests
{
    [Fact]
    public void Success_ContainsValue()
    {
        StageResult result = new StageSuccess<string>(Value: "hello");
        result.Should().BeOfType<StageSuccess<string>>();
        StageSuccess<string> success = (StageSuccess<string>)result;
        success.Value.Should().Be(expected: "hello");
    }

    [Fact]
    public void Failure_ContainsError()
    {
        EncodingError error = new(
            Kind: EncodingErrorKind.InputNotFound,
            Message: "not found",
            FfmpegStderr: null,
            StageName: "Analyze",
            Recoverable: false
        );
        StageResult result = new StageFailure(Error: error);
        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(expected: EncodingErrorKind.InputNotFound);
    }

    [Fact]
    public void PatternMatch_Works()
    {
        StageResult success = new StageSuccess<int>(Value: 42);
        string output = success switch
        {
            StageSuccess<int> s => $"got {s.Value}",
            StageFailure f => $"error: {f.Error.Message}",
            _ => "unknown",
        };
        output.Should().Be(expected: "got 42");
    }
}
