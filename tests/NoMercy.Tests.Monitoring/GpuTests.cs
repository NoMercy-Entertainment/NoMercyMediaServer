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

using FluentAssertions;
using NoMercy.Monitoring;
using Xunit;

namespace NoMercy.Tests.Monitoring;

/// <summary>
/// Requirement: <see cref="Gpu.Index"/> is derived from the provider-assigned
/// <c>Identifier</c> (e.g. "gpu/0"), never a raw field a caller can desync from
/// the identifier. It must resolve to the trailing numeric segment for every
/// real-provider identifier shape, and degrade to 0 — never throw — for any
/// identifier a provider did not (or could not) set correctly, since a thrown
/// exception here would take down the whole resource-collection cycle over one
/// bad GPU entry.
/// </summary>
public class GpuTests
{
    [Theory]
    [InlineData("gpu/0", 0)]
    [InlineData("gpu/1", 1)]
    [InlineData("gpu/12", 12)]
    [InlineData("5", 5)]
    public void Index_ParsesTrailingSegment_ForRealProviderShapes(string identifier, int expected)
    {
        Gpu gpu = new() { Identifier = identifier };

        gpu.Index.Should().Be(expected);
    }

    [Fact]
    public void Index_WithDefaultEmptyIdentifier_DoesNotThrow_ReturnsZero()
    {
        // Regression guard for a real bug: "".Split('/') yields [""] (one empty
        // element, not zero), so LastOrDefault() never falls through to the "0"
        // default and a bare int.Parse("") used to throw FormatException here.
        Gpu gpu = new();

        Action act = () => _ = gpu.Index;

        act.Should().NotThrow();
        gpu.Index.Should().Be(0);
    }

    [Theory]
    [InlineData("gpu/")]
    [InlineData("gpu/abc")]
    [InlineData("not-a-number")]
    public void Index_WithMalformedIdentifier_DoesNotThrow_ReturnsZero(string identifier)
    {
        Gpu gpu = new() { Identifier = identifier };

        Action act = () => _ = gpu.Index;

        act.Should().NotThrow();
        gpu.Index.Should().Be(0);
    }

    [Fact]
    public void Gpu_AllTelemetryFields_RoundTripIndependently()
    {
        Gpu gpu = new()
        {
            Identifier = "gpu/2",
            Name = "NVIDIA GeForce RTX 4090",
            D3D = 45.5,
            Decode = 10.0,
            Core = 55.5,
            Memory = 33.3,
            Encode = 5.0,
            Power = 120.7,
        };

        gpu.Index.Should().Be(2);
        gpu.Name.Should().Be("NVIDIA GeForce RTX 4090");
        gpu.D3D.Should().Be(45.5);
        gpu.Decode.Should().Be(10.0);
        gpu.Core.Should().Be(55.5);
        gpu.Memory.Should().Be(33.3);
        gpu.Encode.Should().Be(5.0);
        gpu.Power.Should().Be(120.7);
    }

    [Fact]
    public void Gpu_DefaultName_IsEmptyNotNull()
    {
        Gpu gpu = new();

        gpu.Name.Should()
            .Be(string.Empty, "a missing name must never surface as null in the API DTO");
    }
}
