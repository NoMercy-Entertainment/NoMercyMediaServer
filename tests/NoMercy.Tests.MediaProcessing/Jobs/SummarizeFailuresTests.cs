// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Database.Models.Encoder;
using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

[Trait("Category", "Unit")]
public sealed class SummarizeFailuresTests
{
    [Fact]
    public void TwoFailedOutcomes_ReturnDistinctKindsAndFirstNonEmptyError()
    {
        List<EncodeTaskOutcome> failed =
        [
            new EncodeTaskOutcome
            {
                TaskId = "t1",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Video",
                ErrorMessage = "x",
                CompletedAt = DateTime.UtcNow,
            },
            new EncodeTaskOutcome
            {
                TaskId = "t2",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Audio",
                ErrorMessage = null,
                CompletedAt = DateTime.UtcNow,
            },
            new EncodeTaskOutcome
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
        List<EncodeTaskOutcome> failedOnly = failed.Where(o => !o.Success).ToList();

        (IReadOnlyList<string> descriptors, string? lastError) = VideoEncodeJob.SummarizeFailures(
            failedOnly
        );

        descriptors.Should().HaveCount(2);
        descriptors.Should().Contain("Video");
        descriptors.Should().Contain("Audio");
        lastError.Should().Be("x");
    }

    [Fact]
    public void DuplicateKind_AggregatesWithCount()
    {
        List<EncodeTaskOutcome> failedOnly =
        [
            new EncodeTaskOutcome
            {
                TaskId = "t1",
                ParentJobId = 1,
                GroupTag = "g",
                Success = false,
                Kind = "Video",
                ErrorMessage = "err1",
                CompletedAt = DateTime.UtcNow,
            },
            new EncodeTaskOutcome
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
            failedOnly
        );

        descriptors.Should().HaveCount(1);
        descriptors[0].Should().Be("Video (2x)");
        lastError.Should().Be("err1");
    }

    [Fact]
    public void AllErrorsNull_ReturnsFallbackMessage()
    {
        List<EncodeTaskOutcome> failedOnly =
        [
            new EncodeTaskOutcome
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
            failedOnly
        );

        lastError.Should().Be("one or more rungs failed");
        descriptors.Should().ContainSingle("Audio");
    }
}
