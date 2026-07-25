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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "Collections")]
public class CollectionMovieDtoTests
{
    private static Movie BuildMovieWithImages(params Image[] images)
    {
        Movie movie = new()
        {
            Id = 7,
            Title = "Collection Member Movie",
            TitleSort = "collection member movie",
            Overview = "A movie inside a collection.",
        };

        foreach (Image image in images)
            movie.Images.Add(image);

        return movie;
    }

    [Fact]
    public void Ctor_MovieWithLogoImage_SetsLogoFromBestVotedLogo()
    {
        Movie movie = BuildMovieWithImages([
                new Image
                {
                    Type = "logo",
                    FilePath = "/logo-low.png",
                    VoteAverage = 1,
                },
                new Image
                {
                    Type = "backdrop",
                    FilePath = "/backdrop.jpg",
                    VoteAverage = 9,
                }
            ]
        );

        CollectionMovieDto dto = new(movie);

        Assert.Equal("/logo-low.png", dto.Logo);
    }

    [Fact]
    public void Ctor_MovieWithoutLogoImage_LeavesLogoNull()
    {
        Movie movie = BuildMovieWithImages(
            new Image
            {
                Type = "backdrop",
                FilePath = "/backdrop.jpg",
                VoteAverage = 9,
            }
        );

        CollectionMovieDto dto = new(movie);

        Assert.Null(dto.Logo);
    }

    [Fact]
    public void Serialized_CollectionMember_EmitsLowercaseLogoKey()
    {
        Movie movie = BuildMovieWithImages(
            new Image
            {
                Type = "logo",
                FilePath = "/logo.png",
                VoteAverage = 5,
            }
        );

        CollectionMovieDto dto = new(movie);

        JObject json = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.True(json.ContainsKey("logo"));
        Assert.Equal("/logo.png", json["logo"]!.Value<string>());
    }
}
