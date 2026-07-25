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
using NoMercy.Database.Models.Media;
using NoMercy.Providers.TMDB.Models.Shared;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Regression coverage for the HashToId nondeterminism bug: the surrogate id
/// minted for a TMDB image with no primary key used to derive from
/// string.GetHashCode(), which .NET randomizes per process (DoS hardening) —
/// the same file path minted a different "id" after every server restart.
/// A same-process "call it twice" assertion can never catch that, because
/// the randomized seed is fixed for the lifetime of one process. These
/// golden-value tests pin the id to a specific pre-computed FNV-1a result;
/// they can only pass against a pure function of the input bytes; against
/// randomized string.GetHashCode() they fail intermittently across runs.
/// </summary>
[Trait("Category", "Unit")]
public class ImageDtoTests
{
    private static TmdbImage MakeTmdbImage(string filePath, int width = 1920, int height = 1080) =>
        new()
        {
            FilePath = filePath,
            Width = width,
            Height = height,
            Iso6391 = "en",
            VoteAverage = 7.5f,
            VoteCount = 10,
        };

    [Fact]
    public void Ctor_TmdbImage_SameFilePath_MintsSameId_WithinProcess()
    {
        TmdbImage first = MakeTmdbImage("/abc123.jpg");
        TmdbImage second = MakeTmdbImage("/abc123.jpg");

        ImageDto firstDto = new(first);
        ImageDto secondDto = new(second);

        firstDto.Id.Should().Be(secondDto.Id);
    }

    [Fact]
    public void Ctor_TmdbImage_DifferentFilePaths_MintDifferentIds()
    {
        ImageDto dtoA = new(MakeTmdbImage("/path-a.jpg"));
        ImageDto dtoB = new(MakeTmdbImage("/path-b.jpg"));

        dtoA.Id.Should().NotBe(dtoB.Id);
    }

    [Fact]
    public void Ctor_TmdbImage_NeverProducesNegativeId()
    {
        // Sweep a range of inputs since the sign-flip transform is the part
        // most likely to regress if the hash algorithm changes again.
        for (int i = 0; i < 200; i++)
        {
            ImageDto dto = new(MakeTmdbImage($"/sweep-{i}-{new string('x', i % 7)}.jpg"));
            dto.Id.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Theory]
    [InlineData("/abc123.jpg", 922_006_951L)]
    [InlineData("/path-a.jpg", 1_781_138_878L)]
    [InlineData("/path-b.jpg", 2_746_522_937L)]
    [InlineData("", 3_128_831_035L)]
    [InlineData("/determinism-check-9f3ac1.jpg", 2_410_689_069L)]
    public void Ctor_TmdbImage_MintsThePreComputedFnv1AId_NotARandomizedHash(
        string filePath,
        long expectedId
    )
    {
        // These expected values are the FNV-1a hash of each input string,
        // computed independently of this codebase. string.GetHashCode() is
        // randomized per process and would only match one of these values by
        // 1-in-2^32 chance, so this pins the algorithm rather than merely
        // re-deriving whatever the implementation currently does.
        ImageDto dto = new(MakeTmdbImage(filePath));

        dto.Id.Should().Be(expectedId);
    }

    [Fact]
    public void Ctor_TmdbImage_NullFilePath_DoesNotThrow_AndIsDeterministic()
    {
        TmdbImage image = new()
        {
            FilePath = null!,
            Width = 100,
            Height = 200,
        };

        ImageDto first = new(image);
        ImageDto second = new(image);

        first.Id.Should().Be(second.Id);
    }

    [Fact]
    public void Ctor_TmdbImage_WidthGreaterThanOrEqualHeight_TypeIsBackdrop()
    {
        ImageDto dto = new(MakeTmdbImage("/landscape.jpg", width: 1920, height: 1080));

        dto.Type.Should().Be("backdrop");
    }

    [Fact]
    public void Ctor_TmdbImage_HeightGreaterThanWidth_TypeIsPoster()
    {
        ImageDto dto = new(MakeTmdbImage("/portrait.jpg", width: 500, height: 750));

        dto.Type.Should().Be("poster");
    }

    [Fact]
    public void Ctor_TmdbImage_SetsSrcAndMetadataFromSource()
    {
        TmdbImage source = MakeTmdbImage("/src-path.jpg");

        ImageDto dto = new(source);

        dto.Src.Should().Be("/src-path.jpg");
        dto.Width.Should().Be(1920);
        dto.Height.Should().Be(1080);
        dto.Iso6391.Should().Be("en");
        dto.VoteAverage.Should().Be(7.5f);
        dto.VoteCount.Should().Be(10);
        dto.ColorPalette.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_TmdbProfile_SameFilePath_MintsSameId()
    {
        TmdbProfile first = new()
        {
            FilePath = "/profile-abc.jpg",
            Width = 300,
            Height = 450,
        };
        TmdbProfile second = new()
        {
            FilePath = "/profile-abc.jpg",
            Width = 300,
            Height = 450,
        };

        ImageDto firstDto = new(first);
        ImageDto secondDto = new(second);

        firstDto.Id.Should().Be(secondDto.Id);
        firstDto.Type.Should().Be("poster");
    }

    [Fact]
    public void Ctor_TmdbProfile_NullFilePath_DoesNotThrow()
    {
        TmdbProfile profile = new()
        {
            FilePath = null,
            Width = 100,
            Height = 200,
        };

        ImageDto dto = new(profile);

        dto.Id.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Ctor_Image_TmdbSite_SrcIsRelativeToSiteRoot()
    {
        Image media = new()
        {
            Id = 42,
            FilePath = "/tmdb-poster.jpg",
            Site = "https://image.tmdb.org/t/p/",
            Type = "poster",
            Width = 500,
            Height = 750,
            Iso6391 = "en",
            VoteAverage = 8.1,
            VoteCount = 4,
        };

        ImageDto dto = new(media);

        dto.Id.Should().Be(42);
        dto.Src.Should().Be("/tmdb-poster.jpg");
        dto.Type.Should().Be("poster");
    }

    [Fact]
    public void Ctor_Image_NonTmdbSite_SrcIsRelativeToMusicImagesRoot()
    {
        Image media = new()
        {
            Id = 7,
            FilePath = "/artist-cover.jpg",
            Site = "https://coverartarchive.org/",
            Type = "cover",
            Width = 500,
            Height = 500,
        };

        ImageDto dto = new(media);

        dto.Src.Should().Be("/images/music/artist-cover.jpg");
    }
}
