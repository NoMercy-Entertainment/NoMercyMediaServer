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

public class StorageMonitorTests
{
    [Fact]
    public void StorageMonitor_Main_ReturnsAtLeastOneEntry()
    {
        List<ResourceMonitorDto> result = StorageMonitor.Main();

        result.Should().NotBeNull();
        result.Should().NotBeEmpty(because: "every host has at least one drive");
    }

    [Fact]
    public void StorageMonitor_Main_EachEntryHasName()
    {
        List<ResourceMonitorDto> result = StorageMonitor.Main();

        foreach (ResourceMonitorDto dto in result)
        {
            dto.Name.Should().NotBeNullOrWhiteSpace(because: "drive name must be populated from DriveInfo");
        }
    }

    [Fact]
    public void StorageMonitor_Main_EachEntryHasType()
    {
        List<ResourceMonitorDto> result = StorageMonitor.Main();

        foreach (ResourceMonitorDto dto in result)
        {
            dto.Type.Should()
                .NotBeNullOrWhiteSpace(because: "drive type is derived from DriveInfo.DriveType.ToString()");
        }
    }

    [Fact]
    public void StorageMonitor_Main_ReadyDrives_HavePositiveTotal()
    {
        List<ResourceMonitorDto> result = StorageMonitor.Main();

        List<ResourceMonitorDto> readyDrives = result.Where(predicate: d => d.Total > 0).ToList();

        readyDrives
            .Should()
            .NotBeEmpty(because: "at least one ready drive must report a positive total size");

        foreach (ResourceMonitorDto dto in readyDrives)
        {
            dto.Total.Should().BeGreaterThan(expected: 0, because: "ready drive has a positive total capacity");
            dto.Available.Should().BeGreaterThanOrEqualTo(expected: 0, because: "available space is never negative");
            dto.Available.Should().BeLessThanOrEqualTo(expected: dto.Total, because: "available cannot exceed total");
        }
    }

    [Fact]
    public void StorageMonitor_Main_ReadyDrives_PercentageIsBetweenZeroAndOneHundred()
    {
        List<ResourceMonitorDto> result = StorageMonitor.Main();

        List<ResourceMonitorDto> readyDrives = result.Where(predicate: d => d.Total > 0).ToList();

        foreach (ResourceMonitorDto dto in readyDrives)
        {
            dto.Percentage.Should()
                .BeInRange(
                    minimumValue: 0f,
                    maximumValue: 100f,
                    because: "Percentage = Available/Total*100 must be in [0, 100] for a ready drive"
                );
        }
    }

    [Fact]
    public void Usage_WhenUsedIsZero_AllCategoriesReturnZero()
    {
        Usage usage = new()
        {
            Used = 0,
            Movies = 1024 * 8,
            Shows = 512 * 8,
            Music = 256 * 8,
        };

        usage
            .Movies.Should()
            .Be(expected: 0, because: "CalculatePercentage guards against division by zero when Used == 0");
        usage.Shows.Should().Be(expected: 0);
        usage.Music.Should().Be(expected: 0);
        usage.Other.Should().Be(expected: 0);
    }

    [Fact]
    public void Usage_WhenUsedIsNonZero_PercentageIsProportional()
    {
        long gbInUnits = 1024 * 8;
        Usage usage = new()
        {
            Used = 10 * gbInUnits,
            Movies = 5 * gbInUnits,
            Shows = 3 * gbInUnits,
            Music = 1 * gbInUnits,
            Other = 1 * gbInUnits,
        };

        usage.Movies.Should().Be(expected: 50, because: "5/10 = 50%");
        usage.Shows.Should().Be(expected: 30, because: "3/10 = 30%");
        usage.Music.Should().Be(expected: 10, because: "1/10 = 10%");
        usage.Other.Should().Be(expected: 10, because: "1/10 = 10%");
    }

    [Fact]
    public void StorageDto_DefaultValues_AreEmpty()
    {
        StorageDto dto = new();

        dto.Path.Should().BeEmpty();
        dto.Data.Should().NotBeNull();
    }
}
