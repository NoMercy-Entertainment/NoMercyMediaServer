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

using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;
using NoMercy.Service.Workers;
using NoMercyQueue;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Tests.Service.Workers;

/// <summary>
/// <see cref="AnimeEnrichmentBackfillStartupService"/> decides, on every boot,
/// whether the one-shot anime enrichment backfill needs dispatching by reading
/// its completion flag from the real app database, mirroring
/// <c>PaletteBackfillStartupServiceTests</c>'s harness for the same reason:
/// <see cref="AppDbContext"/> has no DI seam here. <see cref="QueueRunner.Current"/>
/// is reset to null around each test — the dispatch call is conditional on it
/// (<c>QueueRunner.Current?.</c>) specifically so a not-yet-initialized queue
/// never crashes this hosted service.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnimeEnrichmentBackfillStartupServiceTests : IDisposable
{
    private const string CompleteKey = "anime_enrichment_backfill_complete";

    private static readonly Lock InitLock = new();
    private static bool _dbInitialized;

    private readonly QueueRunner? _originalQueueRunnerCurrent;

    public AnimeEnrichmentBackfillStartupServiceTests()
    {
        EnsureAppDatabase();
        _originalQueueRunnerCurrent = QueueRunner.Current;
        SetQueueRunnerCurrent(null);
    }

    public void Dispose() => SetQueueRunnerCurrent(_originalQueueRunnerCurrent);

    private static void SetQueueRunnerCurrent(QueueRunner? value)
    {
        PropertyInfo property = typeof(QueueRunner).GetProperty(
            nameof(QueueRunner.Current),
            BindingFlags.Public | BindingFlags.Static
        )!;
        property.SetValue(null, value);
    }

    private static void EnsureAppDatabase()
    {
        lock (InitLock)
        {
            if (_dbInitialized)
                return;

            foreach (string path in AppFiles.AllPaths())
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

            ServiceCollection tokenServices = new();
            tokenServices
                .AddDataProtection()
                .PersistKeysToFileSystem(new(AppFiles.DataProtectionKeysDir))
                .SetApplicationName("NoMercyMediaServer");
            ServiceProvider tokenProvider = tokenServices.BuildServiceProvider();
            TokenStore.Initialize(tokenProvider);

            using AppDbContext appContext = new();
            appContext.Database.EnsureCreated();

            _dbInitialized = true;
        }
    }

    private static async Task SetConfigAsync(string key, string value)
    {
        await using AppDbContext db = new();
        ConfigurationModel? existing = await db.Configuration.FirstOrDefaultAsync(c =>
            c.Key == key
        );
        if (existing is null)
            db.Configuration.Add(new() { Key = key, Value = value });
        else
            existing.Value = value;
        await db.SaveChangesAsync();
    }

    private static async Task RemoveConfigAsync(string key)
    {
        await using AppDbContext db = new();
        ConfigurationModel? existing = await db.Configuration.FirstOrDefaultAsync(c =>
            c.Key == key
        );
        if (existing is not null)
        {
            db.Configuration.Remove(existing);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task StartAsync_AlreadyComplete_SkipsDispatchWithoutThrowing()
    {
        await SetConfigAsync(CompleteKey, "true");
        AnimeEnrichmentBackfillStartupService service = new(
            NullLogger<AnimeEnrichmentBackfillStartupService>.Instance
        );

        Exception? thrown = await Record.ExceptionAsync(() =>
            service.StartAsync(CancellationToken.None)
        );

        Assert.Null(thrown);
    }

    [Fact]
    public async Task StartAsync_NotYetComplete_DispatchesWithoutThrowing()
    {
        await RemoveConfigAsync(CompleteKey);
        AnimeEnrichmentBackfillStartupService service = new(
            NullLogger<AnimeEnrichmentBackfillStartupService>.Instance
        );

        Exception? thrown = await Record.ExceptionAsync(() =>
            service.StartAsync(CancellationToken.None)
        );

        Assert.Null(thrown);
    }

    [Fact]
    public async Task StopAsync_DoesNotThrow()
    {
        AnimeEnrichmentBackfillStartupService service = new(
            NullLogger<AnimeEnrichmentBackfillStartupService>.Instance
        );

        Exception? thrown = await Record.ExceptionAsync(() =>
            service.StopAsync(CancellationToken.None)
        );

        Assert.Null(thrown);
    }
}
