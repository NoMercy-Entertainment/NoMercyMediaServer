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
using NoMercy.Database;
using NoMercy.Database.Models.Encoder;
using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

[Trait(name: "Category", value: "Unit")]
public sealed class IncompleteEncodeRecorderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MediaContext _context;
    private readonly IncompleteEncodeRecorder _recorder;

    public IncompleteEncodeRecorderTests()
    {
        string dbName = Guid.NewGuid().ToString();
        _connection = new(connectionString: $"DataSource={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .Options;

        _context = new(options: options);
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw(sql: "PRAGMA foreign_keys = OFF;");

        _recorder = new();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RoundTrip_MissingRenditions_SplitToExpectedCount()
    {
        string[] keys = ["video-1080p", "video-720p", "audio-aac"];

        await _recorder.RecordAsync(
            context: _context,
            mediaId: 42L,
            folderId: "folder-abc",
            title: "Test Movie",
            missingKeys: keys,
            lastError: null,
            attemptsMade: 1,
            ct: CancellationToken.None
        );

        IncompleteEncode? row = await _context
            .IncompleteEncodes.AsNoTracking()
            .FirstOrDefaultAsync(predicate: r => r.MediaId == 42L && r.FolderId == "folder-abc");

        row.Should().NotBeNull();
        string[] split = row!.MissingRenditions.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries);
        split.Should().HaveCount(expected: 3);
        split.Should().BeEquivalentTo(expectation: keys);
    }

    [Fact]
    public async Task RecordAsync_ThenClearAsync_LeavesNoRow()
    {
        await _recorder.RecordAsync(
            context: _context,
            mediaId: 7L,
            folderId: "folder-xyz",
            title: "Some Show S01E01",
            missingKeys: ["video-480p"],
            lastError: "timeout",
            attemptsMade: 2,
            ct: CancellationToken.None
        );

        await _recorder.ClearAsync(
            context: _context,
            mediaId: 7L,
            folderId: "folder-xyz",
            ct: CancellationToken.None
        );

        IncompleteEncode? row = await _context
            .IncompleteEncodes.AsNoTracking()
            .FirstOrDefaultAsync(predicate: r => r.MediaId == 7L && r.FolderId == "folder-xyz");

        row.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_CalledTwice_UpdatesRowNotDuplicates()
    {
        string[] firstKeys = ["video-1080p", "video-720p"];
        string[] secondKeys = ["video-480p"];

        await _recorder.RecordAsync(
            context: _context,
            mediaId: 99L,
            folderId: "folder-dup",
            title: "Dup Movie",
            missingKeys: firstKeys,
            lastError: "error-1",
            attemptsMade: 1,
            ct: CancellationToken.None
        );

        await _recorder.RecordAsync(
            context: _context,
            mediaId: 99L,
            folderId: "folder-dup",
            title: "Dup Movie",
            missingKeys: secondKeys,
            lastError: "error-2",
            attemptsMade: 2,
            ct: CancellationToken.None
        );

        int count = await _context.IncompleteEncodes.CountAsync(predicate: r =>
            r.MediaId == 99L && r.FolderId == "folder-dup"
        );

        count
            .Should()
            .Be(expected: 1, because: "second RecordAsync must update the existing row, not insert a duplicate");

        IncompleteEncode? row = await _context
            .IncompleteEncodes.AsNoTracking()
            .FirstOrDefaultAsync(predicate: r => r.MediaId == 99L && r.FolderId == "folder-dup");

        row!.AttemptsMade.Should().Be(expected: 2);
        row.LastError.Should().Be(expected: "error-2");
        string[] split = row.MissingRenditions.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries);
        split.Should().BeEquivalentTo(expectation: secondKeys);
    }
}
