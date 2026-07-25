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

[Trait("Category", "Unit")]
public sealed class IncompleteEncodeRecorderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MediaContext _context;
    private readonly IncompleteEncodeRecorder _recorder;

    public IncompleteEncodeRecorderTests()
    {
        string dbName = Guid.NewGuid().ToString();
        _connection = new($"DataSource={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new(options);
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

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
            _context,
            42L,
            "folder-abc",
            "Test Movie",
            keys,
            null,
            1,
            CancellationToken.None
        );

        IncompleteEncode? row = await _context
            .IncompleteEncodes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.MediaId == 42L && r.FolderId == "folder-abc");

        row.Should().NotBeNull();
        string[] split = row!.MissingRenditions.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        split.Should().HaveCount(3);
        split.Should().BeEquivalentTo(keys);
    }

    [Fact]
    public async Task RecordAsync_ThenClearAsync_LeavesNoRow()
    {
        await _recorder.RecordAsync(
            _context,
            7L,
            "folder-xyz",
            "Some Show S01E01",
            ["video-480p"],
            "timeout",
            2,
            CancellationToken.None
        );

        await _recorder.ClearAsync(
            _context,
            7L,
            "folder-xyz",
            CancellationToken.None
        );

        IncompleteEncode? row = await _context
            .IncompleteEncodes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.MediaId == 7L && r.FolderId == "folder-xyz");

        row.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_CalledTwice_UpdatesRowNotDuplicates()
    {
        string[] firstKeys = ["video-1080p", "video-720p"];
        string[] secondKeys = ["video-480p"];

        await _recorder.RecordAsync(
            _context,
            99L,
            "folder-dup",
            "Dup Movie",
            firstKeys,
            "error-1",
            1,
            CancellationToken.None
        );

        await _recorder.RecordAsync(
            _context,
            99L,
            "folder-dup",
            "Dup Movie",
            secondKeys,
            "error-2",
            2,
            CancellationToken.None
        );

        int count = await _context.IncompleteEncodes.CountAsync(r =>
            r.MediaId == 99L && r.FolderId == "folder-dup"
        );

        count
            .Should()
            .Be(1, "second RecordAsync must update the existing row, not insert a duplicate");

        IncompleteEncode? row = await _context
            .IncompleteEncodes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.MediaId == 99L && r.FolderId == "folder-dup");

        row!.AttemptsMade.Should().Be(2);
        row.LastError.Should().Be("error-2");
        string[] split = row.MissingRenditions.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        split.Should().BeEquivalentTo(secondKeys);
    }
}
