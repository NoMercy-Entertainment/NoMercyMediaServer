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

using System;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Data.Repositories;
using Xunit;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Unit")]
public class MusicCardDataTests
{
    [Fact]
    public void ArtistCover_EmptyString_ProducesNullCover()
    {
        ArtistCardDto artist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Cover = "",
        };

        MusicCardData card = new(artist: artist);

        Assert.Null(@object: card.Cover);
    }

    [Fact]
    public void ArtistCover_Null_ProducesNullCover()
    {
        ArtistCardDto artist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Cover = null,
        };

        MusicCardData card = new(artist: artist);

        Assert.Null(@object: card.Cover);
    }

    [Fact]
    public void ArtistCover_Path_ProducesImageUrl()
    {
        ArtistCardDto artist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Cover = "/abc.jpg",
        };

        MusicCardData card = new(artist: artist);

        Assert.Equal(expected: "/images/music/abc.jpg", actual: card.Cover);
    }
}
