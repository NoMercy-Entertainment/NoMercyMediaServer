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
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;
using NoMercy.Service.Workers;
using NoMercyQueue;
using Xunit;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Tests.Service.Workers;

/// <summary>
/// <see cref="PaletteBackfillStartupService"/> decides, on every boot, whether
/// the one-shot palette backfill needs (re)dispatching by reading two flags
/// from the real app database — <see cref="AppDbContext"/> has no DI seam here,
/// so this uses the actual configured (NOMERCY_APP_PATH-isolated) database like
/// production does. <see cref="QueueRunner.Current"/> is reset to null around
/// each test: the dispatch calls are conditional on it (<c>QueueRunner.Current?.</c>)
/// specifically so a not-yet-initialized queue never crashes this hosted
/// service — that null-safety is exactly what lets these tests observe the
/// DB-decision logic without needing a live queue.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PaletteBackfillStartupServiceTests : IDisposable
{
    private static readonly Lock InitLock = new();
    private static bool _dbInitialized;

    private readonly QueueRunner? _originalQueueRunnerCurrent;

    public PaletteBackfillStartupServiceTests()
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
    public async Task StartAsync_AlreadyCompleteAtCurrentVersion_SkipsDispatchWithoutThrowing()
    {
        await SetConfigAsync(
            "palette_backfill_version",
            PaletteBackfillJob.CurrentVersion.ToString()
        );
        await SetConfigAsync("palette_backfill_complete", "true");
        PaletteBackfillStartupService service = new(
            NullLogger<PaletteBackfillStartupService>.Instance
        );

        Exception? thrown = await Record.ExceptionAsync(() =>
            service.StartAsync(CancellationToken.None)
        );

        Assert.Null(thrown);
    }

    [Fact]
    public async Task StartAsync_FreshDatabase_ReopensAndDispatchesWithoutThrowing()
    {
        await RemoveConfigAsync("palette_backfill_version");
        await RemoveConfigAsync("palette_backfill_complete");
        PaletteBackfillStartupService service = new(
            NullLogger<PaletteBackfillStartupService>.Instance
        );

        Exception? thrown = await Record.ExceptionAsync(() =>
            service.StartAsync(CancellationToken.None)
        );

        Assert.Null(thrown);

        await using AppDbContext db = new();
        ConfigurationModel? version = await db.Configuration.FirstOrDefaultAsync(c =>
            c.Key == "palette_backfill_version"
        );
        Assert.Equal(PaletteBackfillJob.CurrentVersion.ToString(), version?.Value);
    }

    [Fact]
    public async Task StopAsync_DoesNotThrow()
    {
        PaletteBackfillStartupService service = new(
            NullLogger<PaletteBackfillStartupService>.Instance
        );

        Exception? thrown = await Record.ExceptionAsync(() =>
            service.StopAsync(CancellationToken.None)
        );

        Assert.Null(thrown);
    }
}
