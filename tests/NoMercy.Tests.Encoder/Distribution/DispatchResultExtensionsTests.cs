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

using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// DispatchResult ↔ EncodingResult projection. The dashboard sees the same
/// structured shape regardless of whether the encode ran locally or on a
/// remote worker — divergence here means the orchestrator can't treat local
/// and remote outcomes uniformly.
/// </summary>
public class DispatchResultExtensionsTests
{
    // ── DispatchResult → EncodingResult ────────────────────────────────────

    [Fact]
    public void ToEncodingResult_Success_PopulatesAllFields()
    {
        DispatchResult dispatch = new(
            TaskId: "task-001",
            Success: true,
            OutputPath: "/out/encode",
            Duration: TimeSpan.FromSeconds(120),
            Error: null,
            WorkerId: "worker-A"
        );

        EncodingResult result = dispatch.ToEncodingResult();

        result.Success.Should().BeTrue();
        result.Status.Should().Be("success");
        result.OutputPath.Should().Be("/out/encode");
        result.Duration.Should().Be(TimeSpan.FromSeconds(120));
        result.Error.Should().BeNull();
        result.EnrichedError.Should().BeNull();
        result.JobId.Should().Be("task-001");
    }

    [Fact]
    public void ToEncodingResult_Failure_PopulatesEnrichedError()
    {
        DispatchResult dispatch = new(
            TaskId: "task-002",
            Success: false,
            OutputPath: "",
            Duration: TimeSpan.Zero,
            Error: "ffmpeg returned exit code 1",
            WorkerId: "worker-B"
        );

        EncodingResult result = dispatch.ToEncodingResult();

        result.Success.Should().BeFalse();
        result.Status.Should().Be("failed");
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Be("ffmpeg returned exit code 1");
        result.Error.StageName.Should().Be("worker-B");
        result.Error.Recoverable.Should().BeFalse();
        result.EnrichedError.Should().NotBeNull();
        result.EnrichedError!.Id.Should().Be(EncoderRuleId.EncoderInitFailed);
        result.EnrichedError.Message.Should().Be("ffmpeg returned exit code 1");
    }

    [Fact]
    public void ToEncodingResult_FailureWithNullWorker_UsesRemoteStageName()
    {
        DispatchResult dispatch = new(
            TaskId: "task-003",
            Success: false,
            OutputPath: "",
            Duration: TimeSpan.Zero,
            Error: "OOM during encode"
        );

        EncodingResult result = dispatch.ToEncodingResult();

        result.Error.Should().NotBeNull();
        result.Error!.StageName.Should().Be("remote");
    }

    [Fact]
    public void ToEncodingResult_FailureNullError_NoEnrichedShape()
    {
        DispatchResult dispatch = new(
            TaskId: "task-004",
            Success: false,
            OutputPath: "",
            Duration: TimeSpan.Zero,
            Error: null
        );

        EncodingResult result = dispatch.ToEncodingResult();

        result.Success.Should().BeFalse();
        result.Status.Should().Be("failed");
        // Without a remote-reported error string the projection skips
        // synthesising both legacy + enriched errors. The orchestrator can
        // still surface 'failed' from Status alone.
        result.Error.Should().BeNull();
        result.EnrichedError.Should().BeNull();
    }

    // ── EncodingResult → DispatchResult (reverse leg) ──────────────────────

    [Fact]
    public void ToDispatchResult_Success_StripsTrackingFields()
    {
        EncodingResult result = new(
            Success: true,
            OutputPath: "/out/done",
            Duration: TimeSpan.FromMinutes(2),
            Error: null,
            Metrics: null
        );

        DispatchResult dispatch = result.ToDispatchResult("task-A", workerId: "worker-X");

        dispatch.TaskId.Should().Be("task-A");
        dispatch.Success.Should().BeTrue();
        dispatch.OutputPath.Should().Be("/out/done");
        dispatch.Duration.Should().Be(TimeSpan.FromMinutes(2));
        dispatch.Error.Should().BeNull();
        dispatch.WorkerId.Should().Be("worker-X");
    }

    [Fact]
    public void ToDispatchResult_FailureUsesEnrichedErrorMessage_WhenPresent()
    {
        EncodingResult result = new(
            Success: false,
            OutputPath: "",
            Duration: TimeSpan.Zero,
            Error: new EncodingError(
                Kind: EncodingErrorKind.Unknown,
                Message: "legacy error",
                FfmpegStderr: null,
                StageName: "Build",
                Recoverable: false
            ),
            Metrics: null
        )
        {
            EnrichedError = new EncoderErrorShape(
                Id: EncoderRuleId.EncoderInitFailed,
                Message: "enriched error wins",
                Suggestion: null,
                Details: null
            ),
        };

        DispatchResult dispatch = result.ToDispatchResult("task-B");

        dispatch.Success.Should().BeFalse();
        dispatch.Error.Should().Be("enriched error wins");
    }

    [Fact]
    public void ToDispatchResult_FailureFallsBackToLegacyError_WhenNoEnriched()
    {
        EncodingResult result = new(
            Success: false,
            OutputPath: "",
            Duration: TimeSpan.Zero,
            Error: new EncodingError(
                Kind: EncodingErrorKind.Unknown,
                Message: "legacy only",
                FfmpegStderr: null,
                StageName: "Build",
                Recoverable: false
            ),
            Metrics: null
        );

        DispatchResult dispatch = result.ToDispatchResult("task-C");

        dispatch.Error.Should().Be("legacy only");
    }

    [Fact]
    public void ToDispatchResult_NoWorkerId_StaysNull()
    {
        EncodingResult result = new(
            Success: true,
            OutputPath: "/out",
            Duration: TimeSpan.Zero,
            Error: null,
            Metrics: null
        );

        DispatchResult dispatch = result.ToDispatchResult("task-D");

        dispatch.WorkerId.Should().BeNull();
    }

    // ── Round-trip ─────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Success_PreservesShape()
    {
        DispatchResult original = new(
            TaskId: "task-r1",
            Success: true,
            OutputPath: "/out/r1",
            Duration: TimeSpan.FromSeconds(60),
            WorkerId: "worker-R"
        );

        DispatchResult roundtripped = original
            .ToEncodingResult()
            .ToDispatchResult("task-r1", "worker-R");

        roundtripped.TaskId.Should().Be(original.TaskId);
        roundtripped.Success.Should().Be(original.Success);
        roundtripped.OutputPath.Should().Be(original.OutputPath);
        roundtripped.Duration.Should().Be(original.Duration);
        roundtripped.WorkerId.Should().Be(original.WorkerId);
    }
}
