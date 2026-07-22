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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;
using NoMercy.MediaProcessing.Libraries;

namespace NoMercy.Tests.MediaProcessing.Libraries;

/// <summary>
/// Pins the "add a folder → it gets automatic multi-resolution HLS for free"
/// contract. <see cref="DefaultEncodingPresetLinker"/> is what
/// <c>LibrariesController.AddFolder</c> calls right after a brand-new folder
/// is created — it must attach exactly one default
/// <see cref="EncodingPresetFolder"/> link, never duplicate one on repeat
/// calls, and never touch a folder that already has any link (default or
/// user-picked). That last rule is also the compat guarantee: an existing
/// install's pre-slice folders are never retroactively linked because the
/// controller only invokes this for folders it just created.
/// </summary>
public class DefaultEncodingPresetLinkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MediaContext _context;

    public DefaultEncodingPresetLinkerTests()
    {
        _connection = new(connectionString: "DataSource=:memory:");
        _connection.Open();

        using (SqliteCommand pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .Options;

        _context = new(options: options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Folder NewFolder(Ulid driverId) =>
        new()
        {
            Id = Ulid.NewUlid(),
            Path = $"/media/{Ulid.NewUlid()}",
            DriverId = driverId,
        };

    private EncodingPreset SeedAbrStandardPreset()
    {
        EncodingPreset preset = new()
        {
            // Bound to the constant, not a literal: the linker resolves the
            // default by name, so a rename that missed this test would leave it
            // green while every new folder silently lost auto-encode.
            Id = Ulid.NewUlid(),
            Name = BuiltinPresets.DefaultStreamingPresetName,
            ProfileJson = "{}",
            IsBuiltIn = true,
        };
        _context.EncodingPresets.Add(entity: preset);
        _context.SaveChanges();
        return preset;
    }

    [Fact]
    public async Task AttachDefaultIfMissingAsync_NewFolder_AttachesAbrStandardAsDefault()
    {
        EncodingPreset abrStandard = SeedAbrStandardPreset();
        Folder folder = NewFolder(driverId: Ulid.NewUlid());
        _context.Folders.Add(entity: folder);
        await _context.SaveChangesAsync();

        DefaultEncodingPresetLinker linker = new(
            context: _context,
            logger: NullLogger<DefaultEncodingPresetLinker>.Instance
        );

        bool attached = await linker.AttachDefaultIfMissingAsync(folderId: folder.Id);

        attached.Should().BeTrue(because: "a brand-new folder with no link must get the default attached");

        List<EncodingPresetFolder> links = await _context
            .EncodingPresetFolders.Where(predicate: l => l.FolderId == folder.Id)
            .ToListAsync();

        links.Should().HaveCount(expected: 1);
        links[index: 0].PresetId.Should().Be(expected: abrStandard.Id);
        links[index: 0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task AttachDefaultIfMissingAsync_FolderAlreadyHasAnyLink_DoesNotAttachOrDuplicate()
    {
        // Folder already has a user-picked (non-default) preset link — the
        // linker must leave it alone, whether or not the ABR standard
        // builtin exists.
        SeedAbrStandardPreset();
        EncodingPreset userPreset = new()
        {
            Id = Ulid.NewUlid(),
            Name = "User Picked",
            ProfileJson = "{}",
            IsBuiltIn = false,
        };
        _context.EncodingPresets.Add(entity: userPreset);

        Folder folder = NewFolder(driverId: Ulid.NewUlid());
        _context.Folders.Add(entity: folder);
        _context.EncodingPresetFolders.Add(
            entity: new()
            {
                FolderId = folder.Id,
                PresetId = userPreset.Id,
                IsDefault = false,
            }
        );
        await _context.SaveChangesAsync();

        DefaultEncodingPresetLinker linker = new(
            context: _context,
            logger: NullLogger<DefaultEncodingPresetLinker>.Instance
        );

        bool attached = await linker.AttachDefaultIfMissingAsync(folderId: folder.Id);

        attached.Should().BeFalse(because: "a folder that already has a link must be left untouched");

        List<EncodingPresetFolder> links = await _context
            .EncodingPresetFolders.Where(predicate: l => l.FolderId == folder.Id)
            .ToListAsync();

        links.Should().HaveCount(expected: 1, because: "no duplicate link must be created");
        links[index: 0]
            .PresetId.Should()
            .Be(expected: userPreset.Id, because: "the existing user pick must survive untouched");
    }

    [Fact]
    public async Task AttachDefaultIfMissingAsync_CalledTwiceOnSameNewFolder_LinksOnlyOnce()
    {
        SeedAbrStandardPreset();
        Folder folder = NewFolder(driverId: Ulid.NewUlid());
        _context.Folders.Add(entity: folder);
        await _context.SaveChangesAsync();

        DefaultEncodingPresetLinker linker = new(
            context: _context,
            logger: NullLogger<DefaultEncodingPresetLinker>.Instance
        );

        bool first = await linker.AttachDefaultIfMissingAsync(folderId: folder.Id);
        bool second = await linker.AttachDefaultIfMissingAsync(folderId: folder.Id);

        first.Should().BeTrue();
        second.Should().BeFalse(because: "re-invoking on an already-linked folder must be a no-op");

        List<EncodingPresetFolder> links = await _context
            .EncodingPresetFolders.Where(predicate: l => l.FolderId == folder.Id)
            .ToListAsync();

        links.Should().HaveCount(expected: 1, because: "creating/re-adding must never duplicate the link");
    }

    [Fact]
    public async Task AttachDefaultIfMissingAsync_NoAbrStandardPreset_FallsBackToExistingDefaultLink()
    {
        // The ABR Standard builtin was renamed/removed; some other folder
        // already carries an IsDefault link — reuse that preset instead of
        // leaving the new folder unencoded.
        EncodingPreset fallbackPreset = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Some Other Builtin",
            ProfileJson = "{}",
            IsBuiltIn = true,
        };
        _context.EncodingPresets.Add(entity: fallbackPreset);

        Folder otherFolder = NewFolder(driverId: Ulid.NewUlid());
        _context.Folders.Add(entity: otherFolder);
        _context.EncodingPresetFolders.Add(
            entity: new()
            {
                FolderId = otherFolder.Id,
                PresetId = fallbackPreset.Id,
                IsDefault = true,
            }
        );

        Folder newFolder = NewFolder(driverId: Ulid.NewUlid());
        _context.Folders.Add(entity: newFolder);
        await _context.SaveChangesAsync();

        DefaultEncodingPresetLinker linker = new(
            context: _context,
            logger: NullLogger<DefaultEncodingPresetLinker>.Instance
        );

        bool attached = await linker.AttachDefaultIfMissingAsync(folderId: newFolder.Id);

        attached.Should().BeTrue();

        EncodingPresetFolder link = await _context.EncodingPresetFolders.SingleAsync(predicate: l =>
            l.FolderId == newFolder.Id
        );
        link.PresetId.Should().Be(expected: fallbackPreset.Id);
        link.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task AttachDefaultIfMissingAsync_NoPresetAvailableAtAll_ReturnsFalseAndCreatesNoLink()
    {
        Folder folder = NewFolder(driverId: Ulid.NewUlid());
        _context.Folders.Add(entity: folder);
        await _context.SaveChangesAsync();

        DefaultEncodingPresetLinker linker = new(
            context: _context,
            logger: NullLogger<DefaultEncodingPresetLinker>.Instance
        );

        bool attached = await linker.AttachDefaultIfMissingAsync(folderId: folder.Id);

        attached
            .Should()
            .BeFalse(because: "with no builtin and no existing default link there is nothing to attach");

        bool anyLink = await _context.EncodingPresetFolders.AnyAsync(predicate: l => l.FolderId == folder.Id);
        anyLink.Should().BeFalse();
    }
}
