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

[Trait("Category", "Characterization")]
public class EncoderRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly SqliteConnection _connection;
    private readonly EncoderRepository _repository;

    public EncoderRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(_context);
        _connection = new("Data Source=:memory:");
    }

    [Fact]
    public async Task GetEncoderProfilesAsync_ReturnsAllProfiles()
    {
        List<EncoderProfile> result = await _repository.GetEncoderProfilesAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(p => p.Name == "Default HLS");
    }

    [Fact]
    public async Task GetEncoderProfilesAsync_ReturnsMultipleProfiles()
    {
        _context.EncoderProfiles.Add(
            new()
            {
                Id = Ulid.NewUlid(),
                Name = "H.264",
                Container = "mp4",
            }
        );
        _context.EncoderProfiles.Add(
            new()
            {
                Id = Ulid.NewUlid(),
                Name = "H.265",
                Container = "mkv",
            }
        );
        await _context.SaveChangesAsync();

        List<EncoderProfile> result = await _repository.GetEncoderProfilesAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Should().Contain(p => p.Name == "H.264");
        result.Should().Contain(p => p.Name == "H.265");
    }

    [Fact]
    public async Task GetEncoderProfileByIdAsync_ReturnsProfile_WhenIdExists()
    {
        Ulid profileId = SeedConstants.EncoderProfileId;

        EncoderProfile? result = await _repository.GetEncoderProfileByIdAsync(profileId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Default HLS");
        result.Container.Should().Be("hls");
    }

    [Fact]
    public async Task GetEncoderProfileByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();

        EncoderProfile? result = await _repository.GetEncoderProfileByIdAsync(nonExistentId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddEncoderProfileAsync_InsertsNewProfile()
    {
        Ulid profileId = Ulid.NewUlid();
        EncoderProfile profile = new()
        {
            Id = profileId,
            Name = "New Profile",
            Container = "webm",
            Param = "param_value",
        };

        await _repository.AddEncoderProfileAsync(profile);

        EncoderProfile? persisted = await _repository.GetEncoderProfileByIdAsync(profileId);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("New Profile");
        persisted.Container.Should().Be("webm");
    }

    [Fact]
    public async Task AddEncoderProfileAsync_UpdatesExistingProfile()
    {
        Ulid profileId = SeedConstants.EncoderProfileId;

        await _repository.AddEncoderProfileAsync(
            new()
            {
                Id = profileId,
                Name = "Updated HLS",
                Container = "hls",
                Param = "updated_param",
            }
        );

        EncoderProfile? result = await _repository.GetEncoderProfileByIdAsync(profileId);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated HLS");
    }

    [Fact]
    public async Task DeleteEncoderProfileAsync_RemovesProfile()
    {
        Ulid profileId = Ulid.NewUlid();
        EncoderProfile profile = new()
        {
            Id = profileId,
            Name = "To Delete",
            Container = "mp4",
        };
        _context.EncoderProfiles.Add(profile);
        await _context.SaveChangesAsync();

        await _repository.DeleteEncoderProfileAsync(profile);

        EncoderProfile? result = await _repository.GetEncoderProfileByIdAsync(profileId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEncoderProfileCountAsync_ReturnsCorrectCount()
    {
        int initialCount = await _repository.GetEncoderProfileCountAsync();

        _context.EncoderProfiles.Add(
            new()
            {
                Id = Ulid.NewUlid(),
                Name = "Count Test 1",
                Container = "mp4",
            }
        );
        _context.EncoderProfiles.Add(
            new()
            {
                Id = Ulid.NewUlid(),
                Name = "Count Test 2",
                Container = "mkv",
            }
        );
        await _context.SaveChangesAsync();

        int newCount = await _repository.GetEncoderProfileCountAsync();

        newCount.Should().Be(initialCount + 2);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
