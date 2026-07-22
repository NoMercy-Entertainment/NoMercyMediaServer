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

[Trait(name: "Category", value: "Characterization")]
public class DriverRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly SqliteConnection _connection;
    private readonly DriverRepository _repository;

    public DriverRepositoryTests()
    {
        (_context, _connection) = (
            TestMediaContextFactory.CreateSeededContext(),
            new(connectionString: "Data Source=:memory:")
        );
        _repository = new(context: _context);
    }

    [Fact]
    public async Task GetAllDriversAsync_ReturnsAllDriversWithFolders()
    {
        List<Driver> drivers = await _repository.GetAllDriversAsync();

        drivers.Should().NotBeEmpty();
        drivers.Should().Contain(predicate: d => d.Name == "Local Filesystem");
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
        context.Drivers.Add(entity: newDriver);

        Folder folder = new()
        {
            Id = Ulid.NewUlid(),
            Path = "/media/videos",
            DriverId = newDriverId,
        };
        context.Folders.Add(entity: folder);
        await context.SaveChangesAsync();

        List<Driver> drivers = await _repository.GetAllDriversAsync();

        Driver? addedDriver = drivers.FirstOrDefault(predicate: d => d.Id == newDriverId);
        addedDriver.Should().NotBeNull();
        addedDriver!.Folders.Should().NotBeEmpty();
        addedDriver.Folders.Should().Contain(predicate: f => f.Path == "/media/videos");
    }

    [Fact]
    public async Task GetAllDriversAsync_OrdersByName()
    {
        await using MediaContext context = _context;
        context.Drivers.Add(
            entity: new()
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
            entity: new()
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

        List<string> names = drivers.Select(selector: d => d.Name).ToList();
        names.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetDriverByIdAsync_ReturnsDriver_WhenIdExists()
    {
        Driver? result = await _repository.GetDriverByIdAsync(id: Driver.SystemLocalDriverId);

        result.Should().NotBeNull();
        result!.Name.Should().Be(expected: "Local Filesystem");
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
        context.Drivers.Add(entity: driver);
        context.Folders.Add(
            entity: new()
            {
                Id = Ulid.NewUlid(),
                Path = "/test1",
                DriverId = driverId,
            }
        );
        context.Folders.Add(
            entity: new()
            {
                Id = Ulid.NewUlid(),
                Path = "/test2",
                DriverId = driverId,
            }
        );
        await context.SaveChangesAsync();

        Driver? result = await _repository.GetDriverByIdAsync(id: driverId);

        result.Should().NotBeNull();
        result!.Folders.Should().HaveCount(expected: 2);
    }

    [Fact]
    public async Task GetDriverByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();

        Driver? result = await _repository.GetDriverByIdAsync(id: nonExistentId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DriverExistsAsync_ReturnsTrue_WhenDriverExists()
    {
        bool result = await _repository.DriverExistsAsync(id: Driver.SystemLocalDriverId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DriverExistsAsync_ReturnsFalse_WhenDriverDoesNotExist()
    {
        bool result = await _repository.DriverExistsAsync(id: Ulid.NewUlid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_ReturnsTrue_WhenNameExists()
    {
        bool result = await _repository.NameExistsAsync(name: "Local Filesystem");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_ReturnsFalse_WhenNameDoesNotExist()
    {
        bool result = await _repository.NameExistsAsync(name: "NonExistent Driver Name");

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
        context.Drivers.Add(entity: driver);
        await context.SaveChangesAsync();

        bool result = await _repository.NameExistsAsync(name: "Unique Name", excludeId: driverId);

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
            entity: new()
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
            entity: new()
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

        bool result = await _repository.NameExistsAsync(name: sharedName, excludeId: driverId2);

        result.Should().BeTrue();
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

        Driver result = await _repository.CreateDriverAsync(driver: newDriver);

        result.Id.Should().Be(expected: newDriver.Id);
        result.Name.Should().Be(expected: "New Driver");

        await using MediaContext verify = _context;
        Driver? persisted = await verify.Drivers.FirstOrDefaultAsync(predicate: d => d.Id == newDriver.Id);
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
        context.Drivers.Add(entity: driver);
        await context.SaveChangesAsync();

        driver.Name = "Updated Name";
        await _repository.UpdateDriverAsync(driver: driver);

        await using MediaContext verify = _context;
        Driver? updated = await verify.Drivers.FirstOrDefaultAsync(predicate: d => d.Id == driverId);
        updated!.Name.Should().Be(expected: "Updated Name");
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
        context.Drivers.Add(entity: driver);
        await context.SaveChangesAsync();

        await _repository.DeleteDriverAsync(driver: driver);

        await using MediaContext verify = _context;
        Driver? deleted = await verify.Drivers.FirstOrDefaultAsync(predicate: d => d.Id == driverId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task LibraryFolderCountAsync_IgnoresFoldersNotAttachedToALibrary()
    {
        Ulid driverId = Ulid.NewUlid();
        _context.Drivers.Add(
            entity: new()
            {
                Id = driverId,
                Name = "Detached Driver",
                Type = "s3",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        _context.Folders.Add(
            entity: new()
            {
                Id = Ulid.NewUlid(),
                Path = "/remote/films",
                DriverId = driverId,
            }
        );
        await _context.SaveChangesAsync();

        int count = await _repository.LibraryFolderCountAsync(driverId: driverId);

        count.Should().Be(expected: 0);
    }

    [Fact]
    public async Task LibraryFolderCountAsync_CountsFoldersInUseByALibrary()
    {
        int count = await _repository.LibraryFolderCountAsync(driverId: Driver.SystemLocalDriverId);

        count.Should().Be(expected: 1);
    }

    [Fact]
    public async Task DeleteDriverAsync_CascadesFoldersThatNoLibraryUses()
    {
        Ulid driverId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();
        _context.Drivers.Add(
            entity: new()
            {
                Id = driverId,
                Name = "Orphan Driver",
                Type = "s3",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        _context.Folders.Add(
            entity: new()
            {
                Id = folderId,
                Path = "/remote/films",
                DriverId = driverId,
            }
        );
        await _context.SaveChangesAsync();

        Driver? driver = await _repository.GetDriverByIdAsync(id: driverId);
        Assert.NotNull(@object: driver);

        await _repository.DeleteDriverAsync(driver: driver);

        _context.ChangeTracker.Clear();

        (await _context.Drivers.AnyAsync(predicate: d => d.Id == driverId)).Should().BeFalse();
        (await _context.Folders.AnyAsync(predicate: f => f.Id == folderId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDriverAsync_LeavesFoldersAlone_WhenALibraryStillUsesThem()
    {
        Driver? driver = await _repository.GetDriverByIdAsync(id: Driver.SystemLocalDriverId);
        Assert.NotNull(@object: driver);

        await Assert.ThrowsAnyAsync<Exception>(testCode: () => _repository.DeleteDriverAsync(driver: driver));

        _context.ChangeTracker.Clear();

        (await _context.Folders.AnyAsync(predicate: f => f.Id == SeedConstants.MovieFolderId))
            .Should()
            .BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
