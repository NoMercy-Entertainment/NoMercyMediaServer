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

using NoMercy.NmSystem.Extensions;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait(name: "Category", value: "Unit")]
public class AlphaBucketTests
{
    // 【Oshi No Ko】 -> TitleSort "oshi.no.ko" -> must land under "O", never "#".
    // This is the exact regression the lolomo controllers had: they bucketed on
    // the raw Title (which starts with "【") instead of the normalized TitleSort.
    [Fact]
    public void Matches_OshiNoKo_LandsUnderO_NotHash()
    {
        Assert.True(condition: AlphaBucket.Matches(titleSort: "oshi.no.ko", bucket: "O"));
        Assert.False(condition: AlphaBucket.Matches(titleSort: "oshi.no.ko", bucket: "#"));
    }

    [Theory]
    [InlineData(data: ["matrix", "M"])]
    [InlineData(data: ["inception", "I"])]
    [InlineData(data: ["zelda", "Z"])]
    public void Matches_LetterBucket_IsCaseInsensitive(string titleSort, string bucket)
    {
        Assert.True(condition: AlphaBucket.Matches(titleSort: titleSort, bucket: bucket));
        Assert.True(condition: AlphaBucket.Matches(titleSort: titleSort.ToUpperInvariant(), bucket: bucket.ToLowerInvariant()));
    }

    [Theory]
    [InlineData(data: "1408")] // starts with a digit
    [InlineData(data: "3.body.problem")]
    [InlineData(data: "...and.justice.for.all")] // starts with punctuation
    public void Matches_NonLetterPrefix_LandsUnderHash(string titleSort)
    {
        Assert.True(condition: AlphaBucket.Matches(titleSort: titleSort, bucket: "#"));

        foreach (string bucket in AlphaBucket.Buckets)
        {
            if (bucket == "#")
                continue;

            Assert.False(condition: AlphaBucket.Matches(titleSort: titleSort, bucket: bucket));
        }
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    public void Matches_EmptyTitleSort_LandsUnderHashOnly(string? titleSort)
    {
        Assert.True(condition: AlphaBucket.Matches(titleSort: titleSort, bucket: "#"));
        Assert.False(condition: AlphaBucket.Matches(titleSort: titleSort, bucket: "A"));
    }

    [Fact]
    public void Matches_EveryTitleSort_LandsInExactlyOneBucket()
    {
        string[] titleSorts =
        [
            "oshi.no.ko",
            "matrix",
            "1408",
            "...and.justice.for.all",
            "zelda",
            "",
        ];

        foreach (string titleSort in titleSorts)
        {
            int hits = 0;
            foreach (string bucket in AlphaBucket.Buckets)
            {
                if (AlphaBucket.Matches(titleSort: titleSort, bucket: bucket))
                    hits++;
            }

            Assert.Equal(expected: 1, actual: hits);
        }
    }
}
