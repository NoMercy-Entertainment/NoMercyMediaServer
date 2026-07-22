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

namespace NoMercy.Tests.NmSystem;

[Trait(name: "Category", value: "Unit")]
public class AlphaBucketTests
{
    [Fact]
    public void Buckets_Contains27Elements()
    {
        AlphaBucket.Buckets.Should().HaveCount(expected: 27);
    }

    [Fact]
    public void Buckets_StartsWithHash()
    {
        AlphaBucket.Buckets[0].Should().Be(expected: "#");
    }

    [Fact]
    public void Buckets_ContainsAllLettersAToZ()
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for (int i = 1; i < AlphaBucket.Buckets.Length; i++)
        {
            AlphaBucket.Buckets[i].Should().Be(expected: letters[index: i - 1].ToString());
        }
    }

    [Theory]
    [InlineData(data: [null, "#", true])]
    [InlineData(data: ["", "#", true])]
    [InlineData(data: ["  ", "#", true])]
    public void Matches_EmptyTitleSort_MapsToHash(string? titleSort, string bucket, bool expected)
    {
        AlphaBucket.Matches(titleSort: titleSort, bucket: bucket).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["apple", "A", true])]
    [InlineData(data: ["apple", "B", false])]
    [InlineData(data: ["alice", "A", true])]
    [InlineData(data: ["zebra", "Z", true])]
    [InlineData(data: ["movie", "M", true])]
    public void Matches_LetterBucket_CaseInsensitive(string titleSort, string bucket, bool expected)
    {
        AlphaBucket.Matches(titleSort: titleSort, bucket: bucket).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Apple", "A", true])]
    [InlineData(data: ["APPLE", "A", true])]
    [InlineData(data: ["aPpLe", "A", true])]
    public void Matches_LetterBucket_IgnoresCase(string titleSort, string bucket, bool expected)
    {
        AlphaBucket.Matches(titleSort: titleSort, bucket: bucket).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["1234", "#", true])]
    [InlineData(data: ["1234", "A", false])]
    [InlineData(data: ["#hashtag", "#", true])]
    [InlineData(data: ["@mention", "#", true])]
    [InlineData(data: ["[bracket", "#", true])]
    public void Matches_NonLetterStartingTitle_MapsToHash(
        string titleSort,
        string bucket,
        bool expected
    )
    {
        AlphaBucket.Matches(titleSort: titleSort, bucket: bucket).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["_underscore", "#", true])]
    [InlineData(data: ["-dash", "#", true])]
    [InlineData(data: ["(paren", "#", true])]
    public void Matches_SpecialCharacterStart_MapsToHash(
        string titleSort,
        string bucket,
        bool expected
    )
    {
        AlphaBucket.Matches(titleSort: titleSort, bucket: bucket).Should().Be(expected: expected);
    }

    [Fact]
    public void Matches_HashBucket_OnlyMatchesNonLetterStarts()
    {
        AlphaBucket.Matches(titleSort: "apple", bucket: "#").Should().BeFalse();
        AlphaBucket.Matches(titleSort: "123abc", bucket: "#").Should().BeTrue();
        AlphaBucket.Matches(titleSort: "_apple", bucket: "#").Should().BeTrue();
        AlphaBucket.Matches(titleSort: null, bucket: "#").Should().BeTrue();
    }
}
