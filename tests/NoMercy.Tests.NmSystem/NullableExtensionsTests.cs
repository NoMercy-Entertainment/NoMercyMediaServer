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
public class NullableExtensionsTests
{
    [Fact]
    public void OrEmpty_WithNonNullString_ReturnsString()
    {
        string? value = "test";
        string result = value.OrEmpty();
        result.Should().Be(expected: "test");
    }

    [Fact]
    public void OrEmpty_WithNullString_ReturnsEmptyString()
    {
        string? value = null;
        string result = value.OrEmpty();
        result.Should().Be(expected: string.Empty);
    }

    [Fact]
    public void OrEmpty_WithNonNullArray_ReturnsArray()
    {
        int[]? value = [1, 2, 3];
        int[] result = value.OrEmpty();
        result.Should().Equal(elements: [1, 2, 3]);
    }

    [Fact]
    public void OrEmpty_WithNullArray_ReturnsEmptyArray()
    {
        int[]? value = null;
        int[] result = value.OrEmpty();
        result.Should().BeEmpty();
    }

    [Fact]
    public void OrEmpty_WithNonNullList_ReturnsList()
    {
        List<string>? value = ["a", "b"];
        List<string> result = value.OrEmpty();
        result.Should().Equal(expected: ["a", "b"]);
    }

    [Fact]
    public void OrEmpty_WithNullList_ReturnsEmptyList()
    {
        List<string>? value = null;
        List<string> result = value.OrEmpty();
        result.Should().BeEmpty();
    }

    [Fact]
    public void OrEmpty_WithNonNullEnumerable_ReturnsEnumerable()
    {
        IEnumerable<int>? value = new[] { 1, 2, 3 };
        IEnumerable<int> result = value.OrEmpty();
        result.Should().Equal(elements: [1, 2, 3]);
    }

    [Fact]
    public void OrEmpty_WithNullEnumerable_ReturnsEmptyEnumerable()
    {
        IEnumerable<int>? value = null;
        IEnumerable<int> result = value.OrEmpty();
        result.Should().BeEmpty();
    }

    [Fact]
    public void OrNull_WithNonBlankString_ReturnsString()
    {
        string? value = "test";
        string? result = value.OrNull();
        result.Should().Be(expected: "test");
    }

    [Fact]
    public void OrNull_WithNullString_ReturnsNull()
    {
        string? value = null;
        string? result = value.OrNull();
        result.Should().BeNull();
    }

    [Fact]
    public void OrNull_WithEmptyString_ReturnsNull()
    {
        string? value = "";
        string? result = value.OrNull();
        result.Should().BeNull();
    }

    [Fact]
    public void OrNull_WithWhitespaceOnlyString_ReturnsNull()
    {
        string? value = "   ";
        string? result = value.OrNull();
        result.Should().BeNull();
    }

    [Fact]
    public void OrNull_WithTabAndNewline_ReturnsNull()
    {
        string? value = "\t\n";
        string? result = value.OrNull();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(data: "a")]
    [InlineData(data: "  a  ")]
    [InlineData(data: "abc")]
    public void OrNull_WithNonWhitespaceString_ReturnsString(string value)
    {
        string? input = value;
        string? result = input.OrNull();
        result.Should().Be(expected: value);
    }
}
