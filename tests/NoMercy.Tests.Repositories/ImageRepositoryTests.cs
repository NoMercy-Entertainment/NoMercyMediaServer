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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class ImageRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly SqliteConnection _connection;
    private readonly ImageRepository _repository;

    public ImageRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(context: _context);
        _connection = new(connectionString: "Data Source=:memory:");
    }

    [Fact]
    public async Task GetImageByFilePathAsync_ReturnsImage_WhenFilePathExists()
    {
        Image image = new()
        {
            Id = 1,
            FilePath = "/images/test_image.jpg",
            Type = "poster",
            Iso6391 = "en",
        };
        _context.Images.Add(entity: image);
        await _context.SaveChangesAsync();

        Image? result = await _repository.GetImageByFilePathAsync(filePath: "/images/test_image.jpg");

        result.Should().NotBeNull();
        result!.FilePath.Should().Be(expected: "/images/test_image.jpg");
        result.Id.Should().Be(expected: image.Id);
    }

    [Fact]
    public async Task GetImageByFilePathAsync_ReturnsNull_WhenFilePathDoesNotExist()
    {
        Image? result = await _repository.GetImageByFilePathAsync(filePath: "/nonexistent/path.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetImageByFilePathAsync_ExcludesOtherImages()
    {
        Image image1 = new()
        {
            Id = 2,
            FilePath = "/images/image_one.jpg",
            Type = "poster",
            Iso6391 = "en",
        };
        Image image2 = new()
        {
            Id = 3,
            FilePath = "/images/image_two.jpg",
            Type = "backdrop",
            Iso6391 = "en",
        };
        _context.Images.AddRange(entities: [image1, image2]);
        await _context.SaveChangesAsync();

        Image? result = await _repository.GetImageByFilePathAsync(filePath: "/images/image_one.jpg");

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: image1.Id);
        result.Id.Should().NotBe(unexpected: image2.Id);
    }

    [Fact]
    public async Task GetImageByFilePathAsync_IsCaseSensitive()
    {
        Image image = new()
        {
            Id = 4,
            FilePath = "/images/TestImage.jpg",
            Type = "poster",
            Iso6391 = "en",
        };
        _context.Images.Add(entity: image);
        await _context.SaveChangesAsync();

        Image? result = await _repository.GetImageByFilePathAsync(filePath: "/images/testimage.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetImageByFilePathAsync_ReturnsCorrectImageProperties()
    {
        Image image = new()
        {
            Id = 5,
            FilePath = "/images/complete_image.jpg",
            Type = "logo",
            Iso6391 = "fr",
            VoteAverage = 8.5,
        };
        _context.Images.Add(entity: image);
        await _context.SaveChangesAsync();

        Image? result = await _repository.GetImageByFilePathAsync(filePath: "/images/complete_image.jpg");

        result.Should().NotBeNull();
        result!.Type.Should().Be(expected: "logo");
        result.Iso6391.Should().Be(expected: "fr");
        result.VoteAverage.Should().Be(expected: 8.5);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
