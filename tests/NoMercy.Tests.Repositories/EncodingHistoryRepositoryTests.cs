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

using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class EncodingHistoryRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly EncodingHistoryRepository _repository;

    public EncodingHistoryRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(context: _context);
    }

    [Fact]
    public async Task AddAsync_PersistsEntry()
    {
        EncodingHistory entry = Build();

        await _repository.AddAsync(entry: entry);

        EncodingHistory? loaded = await _repository.GetByIdAsync(id: entry.Id);
        Assert.NotNull(@object: loaded);
        Assert.Equal(expected: entry.InputPath, actual: loaded!.InputPath);
        Assert.Equal(expected: entry.ProfileName, actual: loaded.ProfileName);
    }

    [Fact]
    public async Task GetRecentAsync_OrdersNewestFirst()
    {
        EncodingHistory older = Build(
            profileName: "older",
            createdAt: new DateTime(year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
        );
        EncodingHistory newer = Build(
            profileName: "newer",
            createdAt: new DateTime(year: 2026, month: 6, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
        );

        await _repository.AddAsync(entry: older);
        await _repository.AddAsync(entry: newer);

        List<EncodingHistory> page = await _repository.GetRecentAsync(pageSize: 10, pageIndex: 0);

        Assert.Equal(expected: "newer", actual: page[index: 0].ProfileName);
        Assert.Equal(expected: "older", actual: page[index: 1].ProfileName);
    }

    [Fact]
    public async Task GetRecentAsync_PaginatesCorrectly()
    {
        for (int i = 0; i < 15; i++)
            await _repository.AddAsync(
                entry: Build(profileName: $"profile-{i:D2}", createdAt: DateTime.UtcNow.AddMinutes(value: i))
            );

        List<EncodingHistory> page1 = await _repository.GetRecentAsync(pageSize: 5, pageIndex: 0);
        List<EncodingHistory> page2 = await _repository.GetRecentAsync(pageSize: 5, pageIndex: 1);

        Assert.Equal(expected: 5, actual: page1.Count);
        Assert.Equal(expected: 5, actual: page2.Count);
        Assert.DoesNotContain(collection: page1.Select(selector: p => p.Id), filter: id => page2.Select(selector: p => p.Id).Contains(value: id));
    }

    [Fact]
    public async Task GetRecentAsync_NormalizesBadArguments()
    {
        await _repository.AddAsync(entry: Build());

        List<EncodingHistory> result = await _repository.GetRecentAsync(
            pageSize: -1,
            pageIndex: -5
        );

        Assert.NotEmpty(collection: result);
    }

    [Fact]
    public async Task GetTotalCountAsync_ReturnsRowCount()
    {
        await _repository.AddAsync(entry: Build());
        await _repository.AddAsync(entry: Build());
        await _repository.AddAsync(entry: Build());

        int count = await _repository.GetTotalCountAsync();

        Assert.Equal(expected: 3, actual: count);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        EncodingHistory? loaded = await _repository.GetByIdAsync(id: Ulid.NewUlid());

        Assert.Null(@object: loaded);
    }

    [Fact]
    public async Task DeleteAsync_ExistingRow_Removes()
    {
        EncodingHistory entry = Build();
        await _repository.AddAsync(entry: entry);

        bool removed = await _repository.DeleteAsync(id: entry.Id);

        Assert.True(condition: removed);
        Assert.Null(@object: await _repository.GetByIdAsync(id: entry.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        bool removed = await _repository.DeleteAsync(id: Ulid.NewUlid());

        Assert.False(condition: removed);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOnlyOlderEntries()
    {
        await _repository.AddAsync(entry: Build(createdAt: DateTime.UtcNow.AddDays(value: -60)));
        await _repository.AddAsync(entry: Build(createdAt: DateTime.UtcNow.AddDays(value: -30)));
        await _repository.AddAsync(entry: Build(createdAt: DateTime.UtcNow.AddDays(value: -5)));

        int removed = await _repository.DeleteOlderThanAsync(olderThan: DateTime.UtcNow.AddDays(value: -15));

        Assert.Equal(expected: 2, actual: removed);
        Assert.Equal(expected: 1, actual: await _repository.GetTotalCountAsync());
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesEverything()
    {
        await _repository.AddAsync(entry: Build());
        await _repository.AddAsync(entry: Build());

        int removed = await _repository.DeleteAllAsync();

        Assert.Equal(expected: 2, actual: removed);
        Assert.Equal(expected: 0, actual: await _repository.GetTotalCountAsync());
    }

    [Fact]
    public async Task GetAggregateStatsAsync_EmptyTable_ReturnsZeros()
    {
        EncodingHistoryStats stats = await _repository.GetAggregateStatsAsync();

        Assert.Equal(expected: 0, actual: stats.TotalEncodes);
        Assert.Equal(expected: 0, actual: stats.TotalInputBytes);
        Assert.Equal(expected: 0, actual: stats.TotalOutputBytes);
    }

    [Fact]
    public async Task GetAggregateStatsAsync_AggregatesAcrossRows()
    {
        await _repository.AddAsync(
            entry: Build(profileName: "a", createdAt: DateTime.UtcNow.AddHours(value: -1))
        );
        await _repository.AddAsync(
            entry: Build(profileName: "b", createdAt: DateTime.UtcNow.AddHours(value: -2))
        );

        EncodingHistoryStats stats = await _repository.GetAggregateStatsAsync();

        Assert.Equal(expected: 2, actual: stats.TotalEncodes);
        Assert.Equal(expected: 16_000_000_000, actual: stats.TotalInputBytes);
        Assert.Equal(expected: 8_000_000_000, actual: stats.TotalOutputBytes);
        Assert.Equal(expected: 2.0, actual: stats.AverageSpeed);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(obj: this);
    }

    private static EncodingHistory Build(
        string profileName = "default",
        DateTime? createdAt = null
    ) =>
        new()
        {
            InputPath = "/media/source.mkv",
            OutputPath = "/media/encoded/movie.NoMercy.m3u8",
            ProfileId = Ulid.NewUlid(),
            ProfileName = profileName,
            EncoderUsed = "libx264",
            GpuUsed = null,
            DurationSeconds = 300,
            InputSizeBytes = 8_000_000_000,
            OutputSizeBytes = 4_000_000_000,
            CompressionRatio = 0.5,
            AverageSpeed = 2.0,
            AverageFps = 24.0,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
}
