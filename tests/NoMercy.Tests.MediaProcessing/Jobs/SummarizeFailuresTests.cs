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

using NoMercy.Database.Models.Encoder;
using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

[Trait(name: "Category", value: "Unit")]
public sealed class SummarizeFailuresTests
{
    [Fact]
    public void TwoFailedOutcomes_ReturnDistinctKindsAndFirstNonEmptyError()
    {
        List<EncodeTaskOutcome> failed =
        [
            new()
            {
                TaskId = "t1",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Video",
                ErrorMessage = "x",
                CompletedAt = DateTime.UtcNow,
            },
            new()
            {
                TaskId = "t2",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Audio",
                ErrorMessage = null,
                CompletedAt = DateTime.UtcNow,
            },
            new()
            {
                TaskId = "t3",
                ParentJobId = 1,
                GroupTag = "g",
                Success = true,
                Kind = "Subtitle",
                ErrorMessage = null,
                CompletedAt = DateTime.UtcNow,
            },
        ];

        // SummarizeFailures only receives the already-filtered failed outcomes.
        List<EncodeTaskOutcome> failedOnly = failed.Where(predicate: o => !o.Success).ToList();

        (IReadOnlyList<string> descriptors, string? lastError) = VideoEncodeJob.SummarizeFailures(
            failedOutcomes: failedOnly
        );

        descriptors.Should().HaveCount(expected: 2);
        descriptors.Should().Contain(expected: "Video");
        descriptors.Should().Contain(expected: "Audio");
        lastError.Should().Be(expected: "x");
    }

    [Fact]
    public void DuplicateKind_AggregatesWithCount()
    {
        List<EncodeTaskOutcome> failedOnly =
        [
            new()
            {
                TaskId = "t1",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Video",
                ErrorMessage = "err1",
                CompletedAt = DateTime.UtcNow,
            },
            new()
            {
                TaskId = "t2",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Video",
                ErrorMessage = null,
                CompletedAt = DateTime.UtcNow,
            },
        ];

        (IReadOnlyList<string> descriptors, string? lastError) = VideoEncodeJob.SummarizeFailures(
            failedOutcomes: failedOnly
        );

        descriptors.Should().HaveCount(expected: 1);
        descriptors[index: 0].Should().Be(expected: "Video (2x)");
        lastError.Should().Be(expected: "err1");
    }

    [Fact]
    public void AllErrorsNull_ReturnsFallbackMessage()
    {
        List<EncodeTaskOutcome> failedOnly =
        [
            new()
            {
                TaskId = "t1",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Audio",
                ErrorMessage = null,
                CompletedAt = DateTime.UtcNow,
            },
        ];

        (IReadOnlyList<string> descriptors, string? lastError) = VideoEncodeJob.SummarizeFailures(
            failedOutcomes: failedOnly
        );

        lastError.Should().Be(expected: "one or more rungs failed");
        descriptors.Should().ContainSingle(because: "Audio");
    }
}
