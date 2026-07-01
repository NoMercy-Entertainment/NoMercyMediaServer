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

public class MemoryPercentageTests
{
    [Theory]
    [InlineData(6.0, 2.0, 75.0)]
    [InlineData(0.0, 16.0, 0.0)]
    [InlineData(8.0, 8.0, 50.0)]
    [InlineData(15.0, 1.0, 93.75)]
    public void Memory_Percentage_ComputedCorrectly(
        double use,
        double available,
        double expectedPercent
    )
    {
        Memory memory = new() { Use = use, Available = available };

        memory.Percentage.Should().BeApproximately(expectedPercent, precision: 0.01);
    }

    [Fact]
    public void Memory_Percentage_WhenUseAndAvailableAreZero_ReturnsNanOrZero()
    {
        Memory memory = new() { Use = 0.0, Available = 0.0 };

        double result = memory.Percentage;

        (double.IsNaN(result) || result == 0.0)
            .Should()
            .BeTrue("when total is zero the formula produces NaN or 0, not an exception");
    }

    [Fact]
    public void Memory_Total_IsIndependentOfPercentage()
    {
        Memory memory = new()
        {
            Use = 4.0,
            Available = 12.0,
            Total = 16.0,
        };

        memory.Total.Should().Be(16.0);
        memory.Percentage.Should().BeApproximately(25.0, precision: 0.01);
    }

    [Theory]
    [InlineData(100.0f, 50.0f, 50.0f)]
    [InlineData(200.0f, 100.0f, 50.0f)]
    [InlineData(500.0f, 450.0f, 90.0f)]
    [InlineData(0.0f, 0.0f, float.NaN)]
    public void ResourceMonitorDto_Percentage_ComputedCorrectly(
        float total,
        float available,
        float expectedPercent
    )
    {
        ResourceMonitorDto dto = new() { Total = total, Available = available };

        if (float.IsNaN(expectedPercent))
        {
            float.IsNaN(dto.Percentage).Should().BeTrue("zero total yields NaN");
        }
        else
        {
            dto.Percentage.Should().BeApproximately(expectedPercent, precision: 0.01f);
        }
    }

    [Fact]
    public void ResourceMonitorDto_Name_And_Type_AreMutable()
    {
        ResourceMonitorDto dto = new()
        {
            Name = "C:\\",
            Type = "Fixed",
            Total = 500.0f,
            Available = 300.0f,
        };

        dto.Name.Should().Be("C:\\");
        dto.Type.Should().Be("Fixed");
        dto.Total.Should().Be(500.0f);
        dto.Available.Should().Be(300.0f);
    }

    [Fact]
    public void Resource_DefaultCpu_HasEmptyCoreList()
    {
        Resource resource = new();

        resource.Cpu.Should().NotBeNull();
        resource.Cpu.Core.Should().BeEmpty();
        resource.Cpu.Total.Should().Be(0.0);
        resource.Cpu.Max.Should().Be(0.0);
    }

    [Fact]
    public void Resource_DefaultMemory_HasZeroValues()
    {
        Resource resource = new();

        resource.Memory.Total.Should().Be(0.0);
        resource.Memory.Available.Should().Be(0.0);
        resource.Memory.Use.Should().Be(0.0);
    }

    [Fact]
    public void Cpu_CoreList_AccumulatesCorrectly()
    {
        Cpu cpu = new()
        {
            Total = 55.5,
            Max = 88.0,
            Core =
            [
                new() { Index = 0, Utilization = 70.0 },
                new() { Index = 1, Utilization = 88.0 },
                new() { Index = 2, Utilization = 20.0 },
            ],
        };

        cpu.Total.Should().Be(55.5);
        cpu.Max.Should().Be(88.0);
        cpu.Core.Should().HaveCount(3);
        cpu.Core[1].Utilization.Should().Be(88.0);
        cpu.Core[2].Index.Should().Be(2);
    }
}
