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

namespace NoMercy.Tests.Encoder.Orchestration;

public class EncodingResultTests
{
    [Fact]
    public void Default_construction_has_success_status_and_empty_collections()
    {
        EncodingResult result = new(
            true,
            "/out/test",
            TimeSpan.FromSeconds(10),
            null,
            null
        );

        Assert.Equal("success", result.Status);
        Assert.Equal("", result.JobId);
        Assert.Null(result.Plan);
        Assert.Empty(result.Artifacts);
        Assert.Null(result.Stats);
        Assert.Empty(result.Warnings);
        Assert.Null(result.EnrichedError);
        Assert.True(result.Success);
        Assert.Equal("/out/test", result.OutputPath);
    }

    [Fact]
    public void Failed_status_carries_error_shape_with_catalogued_id()
    {
        EncoderErrorShape shape = new(
            EncoderRuleId.EncoderInitFailed,
            "Encoder 'h264_nvenc' failed to initialise: no device found",
            "Check ffmpeg capability probe.",
            null
        );
        EncodingError legacyError = new(
            EncodingErrorKind.Unknown,
            shape.Message,
            null,
            "Test",
            false
        );

        EncodingResult result = new(
            false,
            string.Empty,
            TimeSpan.Zero,
            legacyError,
            null
        )
        {
            Status = "failed",
            EnrichedError = shape,
        };

        Assert.Equal("failed", result.Status);
        Assert.False(result.Success);
        Assert.NotNull(result.EnrichedError);
        Assert.Equal(EncoderRuleId.EncoderInitFailed, result.EnrichedError!.Id);
        Assert.Contains("h264_nvenc", result.EnrichedError.Message);
        Assert.Empty(result.Artifacts);
        Assert.Null(result.Stats);
    }

    [Fact]
    public void Cancelled_status_has_no_artifacts_or_stats()
    {
        EncodingResult result = new(
            false,
            string.Empty,
            TimeSpan.FromSeconds(3),
            null,
            null
        )
        {
            Status = "cancelled",
        };

        Assert.Equal("cancelled", result.Status);
        Assert.False(result.Success);
        Assert.Empty(result.Artifacts);
        Assert.Null(result.Stats);
        Assert.Null(result.EnrichedError);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Status_round_trips_via_with_expression()
    {
        EncodingResult original = new(
            true,
            "/out/a",
            TimeSpan.FromMinutes(1),
            null,
            null
        )
        {
            Status = "success",
            JobId = "job-001",
        };

        EncodingResult enriched = original with
        {
            Stats = new(
                60.0,
                30.0,
                4000,
                1_000_000_000L,
                500_000_000L
            ),
            Artifacts =
            [
                new(
                    "/out/a/master.m3u8",
                    1024L,
                    "abc123",
                    "application/vnd.apple.mpegurl"
                ),
            ],
        };

        // Original unchanged
        Assert.Equal("success", original.Status);
        Assert.Equal("job-001", original.JobId);
        Assert.Empty(original.Artifacts);
        Assert.Null(original.Stats);

        // Enriched carries new fields
        Assert.Equal("success", enriched.Status);
        Assert.Equal("job-001", enriched.JobId);
        Assert.Single(enriched.Artifacts);
        Assert.NotNull(enriched.Stats);
        Assert.Equal(60.0, enriched.Stats!.DurationSeconds);
        Assert.Equal(30.0, enriched.Stats.AvgFps);
    }
}
