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

using NoMercy.Encoder.Naming;

namespace NoMercy.Tests.Encoder.Naming;

public class MediaKeyResolverTests
{
    private readonly MediaKeyResolver _resolver = new();

    [Theory]
    [InlineData([MediaType.Movie, 550, "mfa"])]
    [InlineData([MediaType.Movie, 1, "m1"])]
    [InlineData([MediaType.Movie, 0, "m0"])]
    [InlineData([MediaType.Episode, 12345, "e9ix"])]
    [InlineData([MediaType.Track, 100, "t2s"])]
    public void ForMedia_ProducesShortKey(MediaType type, long id, string expected)
    {
        _resolver.ForMedia(type, id).Should().Be(expected);
    }

    [Fact]
    public void ForMedia_LargeId_StaysShort()
    {
        // Movies in TMDB top out around 7 digits; 36^5 = 60M covers safely.
        string key = _resolver.ForMedia(MediaType.Movie, 9_999_999);
        key.Length.Should().BeLessThanOrEqualTo(7);
        key[0].Should().Be('m');
    }

    [Fact]
    public void ForMedia_NegativeId_Throws()
    {
        Action act = () => _resolver.ForMedia(MediaType.Movie, -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForMedia_UnknownType_Throws()
    {
        Action act = () => _resolver.ForMedia((MediaType)99, 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForMedia_MaxLongId_DoesNotOverflowStackBuffer()
    {
        // Documents the design ceiling: 13 base-36 digits encode long.MaxValue.
        string key = _resolver.ForMedia(MediaType.Movie, long.MaxValue);
        key.Should().StartWith("m").And.HaveLength(14);
    }
}
