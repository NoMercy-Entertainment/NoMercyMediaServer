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

namespace NoMercy.Tests.NmSystem;

[Trait("Category", "Unit")]
public class MutatorsExtensionsTests
{
    [Fact]
    public void Randomize_WithSingleItem_ReturnsSingle()
    {
        int[] input = [42];
        IEnumerable<int> result = input.Randomize();
        result.Should().Equal(42);
    }

    [Fact]
    public void Randomize_WithMultipleItems_ReturnsAllItems()
    {
        int[] input = [1, 2, 3, 4, 5];
        IEnumerable<int> result = [.. input.Randomize().OrderBy(x => x)];
        result.Should().Equal([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void Randomize_WithEmptySequence_ReturnsEmpty()
    {
        int[] input = [];
        IEnumerable<int> result = input.Randomize();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Randomize_PreservesElementCounts()
    {
        int[] input = [1, 2, 2, 3, 3, 3];
        IEnumerable<int> result = input.Randomize();
        result.Should().HaveCount(6);
        result.Count(x => x == 1).Should().Be(1);
        result.Count(x => x == 2).Should().Be(2);
        result.Count(x => x == 3).Should().Be(3);
    }

    [Fact]
    public void Randomize_WithStrings_Shuffles()
    {
        string[] input = ["apple", "banana", "cherry", "date", "elderberry"];
        IEnumerable<string> result = input.Randomize();
        result.Should().HaveCount(5);
        result.Should().Contain("apple");
        result.Should().Contain("banana");
    }
}
