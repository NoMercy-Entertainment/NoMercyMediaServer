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
public class ConditionalSetExtensionsTests
{
    private class TestClass
    {
        public string Value { get; set; } = "test";
    }

    [Fact]
    public void GetIf_WithTrueConditionAndNonNullSource_ReturnsSource()
    {
        TestClass obj = new();
        TestClass? result = obj.GetIf(condition: true);
        result.Should().Be(expected: obj);
    }

    [Fact]
    public void GetIf_WithFalseConditionAndNonNullSource_ReturnsNull()
    {
        TestClass obj = new();
        TestClass? result = obj.GetIf(condition: false);
        result.Should().BeNull();
    }

    [Fact]
    public void GetIf_WithTrueConditionAndNullSource_ReturnsNull()
    {
        TestClass? obj = null;
        TestClass? result = obj.GetIf(condition: true);
        result.Should().BeNull();
    }

    [Fact]
    public void GetIf_WithFalseConditionAndNullSource_ReturnsNull()
    {
        TestClass? obj = null;
        TestClass? result = obj.GetIf(condition: false);
        result.Should().BeNull();
    }

    [Fact]
    public void GetIfNotNull_WithNonNullSource_ReturnsSource()
    {
        TestClass obj = new();
        TestClass? result = obj.GetIfNotNull();
        result.Should().Be(expected: obj);
    }

    [Fact]
    public void GetIfNotNull_WithNullSource_ReturnsNull()
    {
        TestClass? obj = null;
        TestClass? result = obj.GetIfNotNull();
        result.Should().BeNull();
    }

    [Fact]
    public void GetIf_WithConditionVariable_RespectsDynamicValue()
    {
        TestClass obj = new();
        bool condition = false;
        TestClass? result = obj.GetIf(condition: condition);
        result.Should().BeNull();

        condition = true;
        result = obj.GetIf(condition: condition);
        result.Should().Be(expected: obj);
    }
}
