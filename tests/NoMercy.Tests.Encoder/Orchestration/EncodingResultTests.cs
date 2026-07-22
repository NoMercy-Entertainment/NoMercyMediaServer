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
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Orchestration;

public class EncodingResultTests
{
    [Fact]
    public void Default_construction_has_success_status_and_empty_collections()
    {
        EncodingResult result = new(
            Success: true,
            OutputPath: "/out/test",
            Duration: TimeSpan.FromSeconds(seconds: 10),
            Error: null,
            Metrics: null
        );

        Assert.Equal(expected: "success", actual: result.Status);
        Assert.Equal(expected: "", actual: result.JobId);
        Assert.Null(@object: result.Plan);
        Assert.Empty(collection: result.Artifacts);
        Assert.Null(@object: result.Stats);
        Assert.Empty(collection: result.Warnings);
        Assert.Null(@object: result.EnrichedError);
        Assert.True(condition: result.Success);
        Assert.Equal(expected: "/out/test", actual: result.OutputPath);
    }

    [Fact]
    public void Failed_status_carries_error_shape_with_catalogued_id()
    {
        EncoderErrorShape shape = new(
            Id: EncoderRuleId.EncoderInitFailed,
            Message: "Encoder 'h264_nvenc' failed to initialise: no device found",
            Suggestion: "Check ffmpeg capability probe.",
            Details: null
        );
        EncodingError legacyError = new(
            Kind: EncodingErrorKind.Unknown,
            Message: shape.Message,
            FfmpegStderr: null,
            StageName: "Test",
            Recoverable: false
        );

        EncodingResult result = new(
            Success: false,
            OutputPath: string.Empty,
            Duration: TimeSpan.Zero,
            Error: legacyError,
            Metrics: null
        )
        {
            Status = "failed",
            EnrichedError = shape,
        };

        Assert.Equal(expected: "failed", actual: result.Status);
        Assert.False(condition: result.Success);
        Assert.NotNull(@object: result.EnrichedError);
        Assert.Equal(expected: EncoderRuleId.EncoderInitFailed, actual: result.EnrichedError!.Id);
        Assert.Contains(expectedSubstring: "h264_nvenc", actualString: result.EnrichedError.Message);
        Assert.Empty(collection: result.Artifacts);
        Assert.Null(@object: result.Stats);
    }

    [Fact]
    public void Cancelled_status_has_no_artifacts_or_stats()
    {
        EncodingResult result = new(
            Success: false,
            OutputPath: string.Empty,
            Duration: TimeSpan.FromSeconds(seconds: 3),
            Error: null,
            Metrics: null
        )
        {
            Status = "cancelled",
        };

        Assert.Equal(expected: "cancelled", actual: result.Status);
        Assert.False(condition: result.Success);
        Assert.Empty(collection: result.Artifacts);
        Assert.Null(@object: result.Stats);
        Assert.Null(@object: result.EnrichedError);
        Assert.Null(@object: result.Plan);
    }

    [Fact]
    public void Status_round_trips_via_with_expression()
    {
        EncodingResult original = new(
            Success: true,
            OutputPath: "/out/a",
            Duration: TimeSpan.FromMinutes(minutes: 1),
            Error: null,
            Metrics: null
        )
        {
            Status = "success",
            JobId = "job-001",
        };

        EncodingResult enriched = original with
        {
            Stats = new(
                DurationSeconds: 60.0,
                AvgFps: 30.0,
                OutputBitrateKbps: 4000,
                SourceBytes: 1_000_000_000L,
                OutputBytes: 500_000_000L
            ),
            Artifacts =
            [
                new(
                    Path: "/out/a/master.m3u8",
                    SizeBytes: 1024L,
                    Sha256: "abc123",
                    MediaType: "application/vnd.apple.mpegurl"
                ),
            ],
        };

        // Original unchanged
        Assert.Equal(expected: "success", actual: original.Status);
        Assert.Equal(expected: "job-001", actual: original.JobId);
        Assert.Empty(collection: original.Artifacts);
        Assert.Null(@object: original.Stats);

        // Enriched carries new fields
        Assert.Equal(expected: "success", actual: enriched.Status);
        Assert.Equal(expected: "job-001", actual: enriched.JobId);
        Assert.Single(collection: enriched.Artifacts);
        Assert.NotNull(@object: enriched.Stats);
        Assert.Equal(expected: 60.0, actual: enriched.Stats!.DurationSeconds);
        Assert.Equal(expected: 30.0, actual: enriched.Stats.AvgFps);
    }
}
