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

using System.Xml.Serialization;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Tests.NmSystem;

[Trait(name: "Category", value: "Unit")]
public class XmlHelperTests
{
    [XmlRoot(elementName: "TestObject")]
    public class TestObject
    {
        [XmlElement(elementName: "Name")]
        public string? Name { get; set; }

        [XmlElement(elementName: "Value")]
        public int Value { get; set; }

        public TestObject() { }

        public TestObject(string? name, int value)
        {
            Name = name;
            Value = value;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not TestObject other)
                return false;
            return Name == other.Name && Value == other.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(value1: Name, value2: Value);
        }
    }

    [Fact]
    public void ToXml_SerializesObjectToXml()
    {
        TestObject obj = new(name: "Test", value: 42);
        string xml = obj.ToXml();
        xml.Should().Contain(expected: "TestObject");
        xml.Should().Contain(expected: "Test");
        xml.Should().Contain(expected: "42");
    }

    [Fact]
    public void FromXml_DeserializesXmlToObject()
    {
        string xml = "<TestObject><Name>Test</Name><Value>42</Value></TestObject>";
        TestObject? result = xml.FromXml<TestObject>();
        result.Should().NotBeNull();
        result!.Name.Should().Be(expected: "Test");
        result.Value.Should().Be(expected: 42);
    }

    [Fact]
    public void ToXml_ThenFromXml_RoundTrips()
    {
        TestObject original = new(name: "Round Trip", value: 99);
        string xml = original.ToXml();
        string xmlWithoutBom = xml.TrimStart(trimChar: '﻿');
        TestObject? restored = xmlWithoutBom.FromXml<TestObject>();
        restored.Should().NotBeNull();
        restored!.Name.Should().Be(expected: "Round Trip");
        restored.Value.Should().Be(expected: 99);
    }

    [Fact]
    public void FromXml_WithEmptyString_ReturnsDefault()
    {
        TestObject? result = "".FromXml<TestObject>();
        result.Should().BeNull();
    }

    [Fact]
    public void FromXml_WithNull_ReturnsDefault()
    {
        string? xml = null;
        TestObject? result = xml.FromXml<TestObject>();
        result.Should().BeNull();
    }

    [Fact]
    public void ToXml_WithNullProperties_StillSerializes()
    {
        TestObject obj = new(name: null, value: 5);
        string xml = obj.ToXml();
        xml.Should().Contain(expected: "TestObject");
        xml.Should().Contain(expected: "5");
    }

    [Fact]
    public void FromXml_WithMissingElements_UsesDefaults()
    {
        string xml = "<TestObject><Name>OnlyName</Name></TestObject>";
        TestObject? result = xml.FromXml<TestObject>();
        result.Should().NotBeNull();
        result!.Name.Should().Be(expected: "OnlyName");
        result.Value.Should().Be(expected: 0);
    }
}
