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
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Combined;
using NoMercy.Providers.TMDB.Models.Movies;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "Collections")]
public class CollectionResponseItemDtoTests
{
    [Fact]
    public void Ctor_FromTmdbCollectionAppends_KeepsTranslatedTitleAndOverview()
    {
        TmdbCollectionAppends appends = new()
        {
            Id = 10,
            Name = "The Matrix Collection",
            Overview = "English overview.",
            Parts = [new() { Id = 1, Title = "The Matrix", VoteAverage = 8.7 }],
            Translations = new()
            {
                Translations =
                [
                    new()
                    {
                        Iso6391 = "nl",
                        Data = new()
                        {
                            Title = "De Matrix Collectie",
                            Overview = "Nederlandse samenvatting.",
                        },
                    },
                ],
            },
        };

        CollectionResponseItemDto dto = new(appends);

        Assert.Equal("De Matrix Collectie", dto.Title);
        Assert.Equal("Nederlandse samenvatting.", dto.Overview);
    }

    [Fact]
    public void Ctor_FromTmdbCollectionAppends_FallsBackToEnglish_WhenNoTranslation()
    {
        TmdbCollectionAppends appends = new()
        {
            Id = 11,
            Name = "The Matrix Collection",
            Overview = "English overview.",
            Parts = [new() { Id = 1, Title = "The Matrix", VoteAverage = 8.7 }],
            Translations = new() { Translations = [] },
        };

        CollectionResponseItemDto dto = new(appends);

        Assert.Equal("The Matrix Collection", dto.Title);
        Assert.Equal("English overview.", dto.Overview);
    }
}
