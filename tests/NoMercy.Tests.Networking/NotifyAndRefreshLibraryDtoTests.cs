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

using NoMercy.Networking.Dto;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: NotifyDto must default every field to an empty string (never
/// null) so a client rendering a toast never NREs on an unset field, and
/// RefreshLibraryDto's QueryKey must default to an empty (not null) array so
/// the client's TanStack Query cache-invalidation payload is always iterable.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotifyAndRefreshLibraryDtoTests
{
    [Fact]
    public void NotifyDto_Defaults_AreEmptyStrings()
    {
        NotifyDto dto = new();

        Assert.Equal(string.Empty, dto.Title);
        Assert.Equal(string.Empty, dto.Message);
        Assert.Equal(string.Empty, dto.Type);
    }

    [Fact]
    public void NotifyDto_SetFields_RoundTrip()
    {
        NotifyDto dto = new()
        {
            Title = "Import complete",
            Message = "Spirited Away was added to your library",
            Type = "success",
        };

        Assert.Equal("Import complete", dto.Title);
        Assert.Equal("Spirited Away was added to your library", dto.Message);
        Assert.Equal("success", dto.Type);
    }

    [Fact]
    public void RefreshLibraryDto_Default_QueryKeyIsEmptyArray()
    {
        RefreshLibraryDto dto = new();

        Assert.NotNull(dto.QueryKey);
        Assert.Empty(dto.QueryKey);
    }

    [Fact]
    public void RefreshLibraryDto_SetQueryKey_RoundTrips()
    {
        RefreshLibraryDto dto = new() { QueryKey = ["movies", 129, null] };

        Assert.Equal(3, dto.QueryKey.Length);
        Assert.Equal("movies", dto.QueryKey[0]);
        Assert.Equal(129, dto.QueryKey[1]);
        Assert.Null(dto.QueryKey[2]);
    }
}
