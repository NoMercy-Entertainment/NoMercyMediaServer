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

using NoMercy.Api.DTOs.Media;
using Xunit;

namespace NoMercy.Tests.Api.Dtos;

/// <summary>
/// take was unbounded (default 300, no cap), so any authenticated caller could
/// force a huge query/materialization with ?take=500000. Take is now clamped.
/// </summary>
public class PageRequestDtoTests
{
    [Theory]
    [InlineData(data: [50, 50])]
    [InlineData(data: [1000, 1000])]
    [InlineData(data: [500000, 1000])]
    [InlineData(data: [0, 300])]
    [InlineData(data: [-5, 300])]
    public void Take_IsClampedToRange(int input, int expected)
    {
        PageRequestDto dto = new() { Take = input };

        Assert.Equal(expected: expected, actual: dto.Take);
    }

    [Fact]
    public void Take_DefaultsTo300()
    {
        Assert.Equal(expected: 300, actual: new PageRequestDto().Take);
    }

    [Theory]
    [InlineData(data: [5, 5])]
    [InlineData(data: [0, 0])]
    [InlineData(data: [-3, 0])]
    public void Page_FloorsAtZero(int input, int expected)
    {
        PageRequestDto dto = new() { Page = input };

        Assert.Equal(expected: expected, actual: dto.Page);
    }
}
