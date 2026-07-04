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
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class DriverRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly SqliteConnection _connection;
    private readonly DriverRepository _repository;

    public DriverRepositoryTests()
    {
        (_context, _connection) = (
            TestMediaContextFactory.CreateSeededContext(),
            new SqliteConnection("Data Source=:memory:")
        );
        _repository = new(_context);
    }

    [Fact]
    public async Task GetAllDriversAsync_ReturnsAllDriversWithFolders()
    {
        List<Driver> drivers = await _repository.GetAllDriversAsync();

        drivers.Should().NotBeEmpty();
        drivers.Should().Contain(d => d.Name == "Local Filesystem");
    }

    [Fact]
    public async Task GetAllDriversAsync_IncludesFolders()
    {
        await using MediaContext context = _context;
        Ulid newDriverId = Ulid.NewUlid();
        Driver newDriver = new()
        {
            Id = newDriverId,
            Name = "Secondary Driver",
            Type = "local",
            Config = """{"rootPath":"/media"}""",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(newDriver);

        Folder folder = new()
        {
            Id = Ulid.NewUlid(),
            Path = "/media/videos",
            DriverId = newDriverId,
        };
        context.Folders.Add(folder);
        await context.SaveChangesAsync();

        List<Driver> drivers = await _repository.GetAllDriversAsync();

        Driver? addedDriver = drivers.FirstOrDefault(d => d.Id == newDriverId);
        addedDriver.Should().NotBeNull();
        addedDriver!.Folders.Should().NotBeEmpty();
        addedDriver.Folders.Should().Contain(f => f.Path == "/media/videos");
    }

    [Fact]
    public async Task GetAllDriversAsync_OrdersByName()
    {
        await using MediaContext context = _context;
        context.Drivers.Add(
            new Driver
            {
                Id = Ulid.NewUlid(),
                Name = "Zebra Drive",
                Type = "local",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        context.Drivers.Add(
            new Driver
            {
                Id = Ulid.NewUlid(),
                Name = "Alpha Drive",
                Type = "local",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        await context.SaveChangesAsync();

        List<Driver> drivers = await _repository.GetAllDriversAsync();

        List<string> names = drivers.Select(d => d.Name).ToList();
        names.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetDriverByIdAsync_ReturnsDriver_WhenIdExists()
    {
        Driver? result = await _repository.GetDriverByIdAsync(Driver.SystemLocalDriverId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Local Filesystem");
    }

    [Fact]
    public async Task GetDriverByIdAsync_IncludesFolders()
    {
        await using MediaContext context = _context;
        Ulid driverId = Ulid.NewUlid();
        Driver driver = new()
        {
            Id = driverId,
            Name = "Test Driver",
            Type = "local",
            Config = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(driver);
        context.Folders.Add(
            new Folder
            {
                Id = Ulid.NewUlid(),
                Path = "/test1",
                DriverId = driverId,
            }
        );
        context.Folders.Add(
            new Folder
            {
                Id = Ulid.NewUlid(),
                Path = "/test2",
                DriverId = driverId,
            }
        );
        await context.SaveChangesAsync();

        Driver? result = await _repository.GetDriverByIdAsync(driverId);

        result.Should().NotBeNull();
        result!.Folders.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDriverByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();

        Driver? result = await _repository.GetDriverByIdAsync(nonExistentId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DriverExistsAsync_ReturnsTrue_WhenDriverExists()
    {
        bool result = await _repository.DriverExistsAsync(Driver.SystemLocalDriverId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DriverExistsAsync_ReturnsFalse_WhenDriverDoesNotExist()
    {
        bool result = await _repository.DriverExistsAsync(Ulid.NewUlid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_ReturnsTrue_WhenNameExists()
    {
        bool result = await _repository.NameExistsAsync("Local Filesystem");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_ReturnsFalse_WhenNameDoesNotExist()
    {
        bool result = await _repository.NameExistsAsync("NonExistent Driver Name");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_ExcludesSpecifiedId()
    {
        await using MediaContext context = _context;
        Ulid driverId = Ulid.NewUlid();
        Driver driver = new()
        {
            Id = driverId,
            Name = "Unique Name",
            Type = "local",
            Config = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        bool result = await _repository.NameExistsAsync("Unique Name", driverId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_FindsOtherDriversWithSameName()
    {
        await using MediaContext context = _context;
        Ulid driverId1 = Ulid.NewUlid();
        Ulid driverId2 = Ulid.NewUlid();
        string sharedName = "Shared Name " + Guid.NewGuid();
        context.Drivers.Add(
            new Driver
            {
                Id = driverId1,
                Name = sharedName,
                Type = "local",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        context.Drivers.Add(
            new Driver
            {
                Id = driverId2,
                Name = sharedName + "_other",
                Type = "local",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        await context.SaveChangesAsync();

        bool result = await _repository.NameExistsAsync(sharedName, driverId2);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task FolderCountAsync_ReturnsCorrectCount()
    {
        await using MediaContext context = _context;
        Ulid driverId = Ulid.NewUlid();
        Driver driver = new()
        {
            Id = driverId,
            Name = "Count Test Driver",
            Type = "local",
            Config = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(driver);
        context.Folders.Add(
            new Folder
            {
                Id = Ulid.NewUlid(),
                Path = "/path1",
                DriverId = driverId,
            }
        );
        context.Folders.Add(
            new Folder
            {
                Id = Ulid.NewUlid(),
                Path = "/path2",
                DriverId = driverId,
            }
        );
        context.Folders.Add(
            new Folder
            {
                Id = Ulid.NewUlid(),
                Path = "/path3",
                DriverId = driverId,
            }
        );
        await context.SaveChangesAsync();

        int count = await _repository.FolderCountAsync(driverId);

        count.Should().Be(3);
    }

    [Fact]
    public async Task FolderCountAsync_ReturnsZero_WhenNoFoldersExist()
    {
        await using MediaContext context = _context;
        Ulid driverId = Ulid.NewUlid();
        Driver driver = new()
        {
            Id = driverId,
            Name = "Empty Driver",
            Type = "local",
            Config = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        int count = await _repository.FolderCountAsync(driverId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CreateDriverAsync_PersistsDriver()
    {
        Driver newDriver = new()
        {
            Id = Ulid.NewUlid(),
            Name = "New Driver",
            Type = "network",
            Config = """{"host":"192.168.1.1"}""",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        Driver result = await _repository.CreateDriverAsync(newDriver);

        result.Id.Should().Be(newDriver.Id);
        result.Name.Should().Be("New Driver");

        await using MediaContext verify = _context;
        Driver? persisted = await verify.Drivers.FirstOrDefaultAsync(d => d.Id == newDriver.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateDriverAsync_PersistsChanges()
    {
        await using MediaContext context = _context;
        Ulid driverId = Ulid.NewUlid();
        Driver driver = new()
        {
            Id = driverId,
            Name = "Original Name",
            Type = "local",
            Config = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        driver.Name = "Updated Name";
        await _repository.UpdateDriverAsync(driver);

        await using MediaContext verify = _context;
        Driver? updated = await verify.Drivers.FirstOrDefaultAsync(d => d.Id == driverId);
        updated!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteDriverAsync_RemovesDriver()
    {
        await using MediaContext context = _context;
        Ulid driverId = Ulid.NewUlid();
        Driver driver = new()
        {
            Id = driverId,
            Name = "To Delete",
            Type = "local",
            Config = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        await _repository.DeleteDriverAsync(driver);

        await using MediaContext verify = _context;
        Driver? deleted = await verify.Drivers.FirstOrDefaultAsync(d => d.Id == driverId);
        deleted.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
