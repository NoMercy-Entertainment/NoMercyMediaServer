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
using NoMercy.Data.DTOs;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class FolderRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly SqliteConnection _connection;
    private readonly FolderRepository _repository;

    public FolderRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(context: _context);
        _connection = new(connectionString: "Data Source=:memory:");
    }

    [Fact]
    public async Task GetFolderByIdAsync_ReturnsFolderWithDriver()
    {
        Folder? result = await _repository.GetFolderByIdAsync(folderId: SeedConstants.MovieFolderId);

        result.Should().NotBeNull();
        result!.Path.Should().Be(expected: "/media/movies");
        result.Driver.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFolderByIdAsync_ReturnsFolderWithLibraries()
    {
        Folder? result = await _repository.GetFolderByIdAsync(folderId: SeedConstants.MovieFolderId);

        result.Should().NotBeNull();
        result!.FolderLibraries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetFolderByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        Folder? result = await _repository.GetFolderByIdAsync(folderId: Ulid.NewUlid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFolderByPathAsync_ReturnsFolderByPath()
    {
        Folder? result = await _repository.GetFolderByPathAsync(requestPath: "/media/movies");

        result.Should().NotBeNull();
        result!.Path.Should().Be(expected: "/media/movies");
    }

    [Fact]
    public async Task GetFolderByPathAsync_ReturnsNull_WhenPathDoesNotExist()
    {
        Folder? result = await _repository.GetFolderByPathAsync(requestPath: "/nonexistent/path");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFolderByDriverAndPathAsync_ReturnsFolderByComposite()
    {
        Folder? result = await _repository.GetFolderByDriverAndPathAsync(
            driverId: Driver.SystemLocalDriverId,
            requestPath: "/media/movies"
        );

        result.Should().NotBeNull();
        result!.Path.Should().Be(expected: "/media/movies");
        result.DriverId.Should().Be(expected: Driver.SystemLocalDriverId);
    }

    [Fact]
    public async Task GetFolderByDriverAndPathAsync_ExcludesOtherDrivers()
    {
        Ulid otherDriverId = Ulid.NewUlid();
        _context.Drivers.Add(
            entity: new()
            {
                Id = otherDriverId,
                Name = "Other Driver",
                Type = "network",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        _context.Folders.Add(
            entity: new()
            {
                Id = Ulid.NewUlid(),
                Path = "/media/movies",
                DriverId = otherDriverId,
            }
        );
        await _context.SaveChangesAsync();

        Folder? result = await _repository.GetFolderByDriverAndPathAsync(
            driverId: otherDriverId,
            requestPath: "/media/movies"
        );

        result.Should().NotBeNull();
        result!.DriverId.Should().Be(expected: otherDriverId);

        Folder? wrongDriver = await _repository.GetFolderByDriverAndPathAsync(
            driverId: Driver.SystemLocalDriverId,
            requestPath: "/media/movies"
        );
        wrongDriver!.DriverId.Should().Be(expected: Driver.SystemLocalDriverId);
    }

    [Fact]
    public async Task GetFoldersByLibraryIdAsync_WithDtos_ReturnsFolders()
    {
        FolderLibraryDto[] dtos = new[]
        {
            new FolderLibraryDto { FolderId = SeedConstants.MovieFolderId },
        };

        List<Folder> result = await _repository.GetFoldersByLibraryIdAsync(folderLibraries: dtos);

        result.Should().NotBeEmpty();
        result.Should().Contain(predicate: f => f.Id == SeedConstants.MovieFolderId);
    }

    [Fact]
    public async Task GetFoldersByLibraryIdAsync_WithUlid_ReturnsFoldersByLibrary()
    {
        List<Folder> result = await _repository.GetFoldersByLibraryIdAsync(
            libraryId: SeedConstants.MovieLibraryId
        );

        result.Should().NotBeEmpty();
        result.Should().Contain(predicate: f => f.Id == SeedConstants.MovieFolderId);
    }

    [Fact]
    public async Task GetFoldersByLibraryIdAsync_ReturnsMultipleFolders()
    {
        Ulid folder2Id = Ulid.NewUlid();
        _context.Folders.Add(
            entity: new()
            {
                Id = folder2Id,
                Path = "/media/tv",
                DriverId = Driver.SystemLocalDriverId,
            }
        );
        _context.FolderLibrary.Add(entity: new(folderId: folder2Id, libraryId: SeedConstants.MovieLibraryId));
        await _context.SaveChangesAsync();

        List<Folder> result = await _repository.GetFoldersByLibraryIdAsync(
            libraryId: SeedConstants.MovieLibraryId
        );

        result.Should().HaveCountGreaterThanOrEqualTo(expected: 2);
    }

    [Fact]
    public async Task GetFolderById_ReturnsFolderWithoutRelations()
    {
        Folder? result = await _repository.GetFolderById(folderId: SeedConstants.MovieFolderId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: SeedConstants.MovieFolderId);
    }

    [Fact]
    public async Task GetFolderByPath_ReturnsFolderWithoutRelations()
    {
        Folder? result = await _repository.GetFolderByPath(path: "/media/movies");

        result.Should().NotBeNull();
        result!.Path.Should().Be(expected: "/media/movies");
    }

    [Fact]
    public async Task AddFolderAsync_InsertsNewFolder()
    {
        Ulid newFolderId = Ulid.NewUlid();
        Folder folder = new()
        {
            Id = newFolderId,
            Path = "/new/folder",
            DriverId = Driver.SystemLocalDriverId,
        };

        await _repository.AddFolderAsync(folder: folder);

        Folder? result = await _repository.GetFolderById(folderId: newFolderId);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AddFolderAsync_UpsertsByCompositeKey()
    {
        Ulid existingFolderId = SeedConstants.MovieFolderId;
        Folder update = new()
        {
            Id = existingFolderId,
            Path = "/media/movies",
            DriverId = Driver.SystemLocalDriverId,
        };

        await _repository.AddFolderAsync(folder: update);

        List<Folder> allFolders = await _context.Folders.ToListAsync();
        allFolders.Should().NotContain(predicate: f => f.Id != existingFolderId && f.Path == "/media/movies");
    }

    [Fact]
    public async Task AddFolderLibraryAsync_Single_UpsertsFolderLibrary()
    {
        Ulid folderId = Ulid.NewUlid();
        _context.Folders.Add(
            entity: new()
            {
                Id = folderId,
                Path = "/test",
                DriverId = Driver.SystemLocalDriverId,
            }
        );
        await _context.SaveChangesAsync();

        FolderLibrary fl = new(folderId: folderId, libraryId: SeedConstants.MovieLibraryId);
        await _repository.AddFolderLibraryAsync(folderLibrary: fl);

        FolderLibrary? result = await _context.FolderLibrary.FirstOrDefaultAsync(predicate: x =>
            x.FolderId == folderId && x.LibraryId == SeedConstants.MovieLibraryId
        );
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AddFolderLibraryAsync_Multiple_UpsertsMultiple()
    {
        Ulid folder1Id = Ulid.NewUlid();
        Ulid folder2Id = Ulid.NewUlid();
        _context.Folders.AddRange(entities:
            [
                new Folder
                {
                    Id = folder1Id,
                    Path = "/test1",
                    DriverId = Driver.SystemLocalDriverId,
                },
                new Folder
                {
                    Id = folder2Id,
                    Path = "/test2",
                    DriverId = Driver.SystemLocalDriverId,
                }
            ]
        );
        await _context.SaveChangesAsync();

        FolderLibrary[] fls = new[]
        {
            new FolderLibrary(folderId: folder1Id, libraryId: SeedConstants.MovieLibraryId),
            new FolderLibrary(folderId: folder2Id, libraryId: SeedConstants.MovieLibraryId),
        };
        await _repository.AddFolderLibraryAsync(folderLibraries: fls);

        List<FolderLibrary> results = await _context
            .FolderLibrary.Where(predicate: x =>
                x.LibraryId == SeedConstants.MovieLibraryId
                && (x.FolderId == folder1Id || x.FolderId == folder2Id)
            )
            .ToListAsync();
        results.Should().HaveCount(expected: 2);
    }

    [Fact]
    public async Task UpdateFolderAsync_PersistsChanges()
    {
        Folder folder = await _context.Folders.FirstAsync(predicate: f => f.Id == SeedConstants.MovieFolderId);
        folder.Path = "/updated/path";

        await _repository.UpdateFolderAsync(folder: folder);

        Folder? updated = await _context.Folders.FirstOrDefaultAsync(predicate: f =>
            f.Id == SeedConstants.MovieFolderId
        );
        updated!.Path.Should().Be(expected: "/updated/path");
    }

    [Fact]
    public async Task DeleteFolderAsync_RemovesFolder()
    {
        Ulid folderId = Ulid.NewUlid();
        Folder folder = new()
        {
            Id = folderId,
            Path = "/to/delete",
            DriverId = Driver.SystemLocalDriverId,
        };
        _context.Folders.Add(entity: folder);
        await _context.SaveChangesAsync();

        await _repository.DeleteFolderAsync(folder: folder);

        Folder? result = await _context.Folders.FirstOrDefaultAsync(predicate: f => f.Id == folderId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFolderAsync_WithForeignKeyDependents_SucceedsWithoutThrowing()
    {
        // Real DB-level FK dependents (FolderLibrary, EncodingPresetFolder) with the
        // folder fetched through a context that has never tracked them. The
        // set-based ExecuteDelete must remove the dependents before the folder so
        // SQLite's Restrict constraint does not throw.
        (IDbContextFactory<MediaContext> factory, SqliteConnection keepAlive) =
            TestMediaContextFactory.CreateSeededFactory();
        try
        {
            await using MediaContext deleteContext = factory.CreateDbContext();
            FolderRepository isolatedRepository = new(context: deleteContext);

            Folder folder = await deleteContext.Folders.FirstAsync(predicate: f =>
                f.Id == SeedConstants.MovieFolderId
            );

            Func<Task> act = async () => await isolatedRepository.DeleteFolderAsync(folder: folder);

            await act.Should().NotThrowAsync();

            await using MediaContext verifyContext = factory.CreateDbContext();
            Folder? result = await verifyContext.Folders.FirstOrDefaultAsync(predicate: f =>
                f.Id == SeedConstants.MovieFolderId
            );
            result.Should().BeNull();
        }
        finally
        {
            keepAlive.Dispose();
        }
    }

    [Fact]
    public async Task DeleteFolderAsync_WhenFolderLoadedWithIncludedLibraries_DoesNotThrow()
    {
        // Reproduces the production failure: GetFolderByIdAsync Includes
        // FolderLibraries, so the returned folder is tracked WITH its required
        // children. A tracked Remove marks that required relationship severed and
        // throws HandleConceptualNulls in the change tracker before any SQL runs —
        // which a DB-level PRAGMA foreign_keys=OFF cannot prevent. The set-based
        // delete must survive a folder loaded with its dependents.
        Folder folder = (await _repository.GetFolderByIdAsync(folderId: SeedConstants.MovieFolderId))!;
        folder.FolderLibraries.Should().NotBeEmpty();

        Func<Task> act = async () => await _repository.DeleteFolderAsync(folder: folder);
        await act.Should().NotThrowAsync();

        Folder? deleted = await _context.Folders.FirstOrDefaultAsync(predicate: f =>
            f.Id == SeedConstants.MovieFolderId
        );
        deleted.Should().BeNull();

        bool orphanLinks = await _context.FolderLibrary.AnyAsync(predicate: fl =>
            fl.FolderId == SeedConstants.MovieFolderId
        );
        orphanLinks.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllFoldersAsync_ReturnsAllFolders()
    {
        List<Folder> result = await _repository.GetAllFoldersAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(predicate: f => f.Path == "/media/movies");
    }

    [Fact]
    public async Task SyncFolderLibraryAsync_DeletesOldAndInsertsNew()
    {
        Ulid folderId = Ulid.NewUlid();
        Folder folder = new()
        {
            Id = folderId,
            Path = "/sync/test",
            DriverId = Driver.SystemLocalDriverId,
        };
        _context.Folders.Add(entity: folder);
        _context.FolderLibrary.Add(entity: new(folderId: folderId, libraryId: SeedConstants.MovieLibraryId));
        await _context.SaveChangesAsync();

        FolderLibrary[] newFls = new[] { new FolderLibrary(folderId: folderId, libraryId: SeedConstants.TvLibraryId) };
        await _repository.SyncFolderLibraryAsync(folderLibraries: newFls, folders: new() { folder });

        FolderLibrary? oldMapping = await _context.FolderLibrary.FirstOrDefaultAsync(predicate: x =>
            x.FolderId == folderId && x.LibraryId == SeedConstants.MovieLibraryId
        );
        FolderLibrary? newMapping = await _context.FolderLibrary.FirstOrDefaultAsync(predicate: x =>
            x.FolderId == folderId && x.LibraryId == SeedConstants.TvLibraryId
        );

        oldMapping.Should().BeNull();
        newMapping.Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
