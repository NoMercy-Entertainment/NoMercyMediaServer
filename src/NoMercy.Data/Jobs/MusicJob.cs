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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Data.Services;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.AcoustId;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Data.Jobs;

[Serializable]
public class MusicJob : IShouldQueue, IJobStorageInjector, IDisposable, IAsyncDisposable
{
    [JsonIgnore]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [JsonIgnore]
    private ILogger Log => field ??= LoggerFactory.CreateLogger(GetType());

    private readonly MediaContext _mediaContext = new();

    public string QueueName => "import";
    public int Priority => 5;

    public string? Folder { get; set; }
    public Library? Library { get; set; }

    [JsonIgnore]
    public IStorageFactory StorageFactory { get; set; } = null!;

    [JsonIgnore]
    public IStorageDriver storageDriver { get; set; } = null!;

    [JsonIgnore]
    public IAudioFingerprinter AudioFingerprinter { get; set; } = null!;

    private ILogger<MusicLogic> _musicLogicLogger = null!;

    public MusicJob()
    {
        //
    }

    public MusicJob(string folder, Library library)
    {
        Folder = folder;
        Library = library;
    }

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        StorageFactory = serviceProvider.GetRequiredService<IStorageFactory>();
        storageDriver = serviceProvider.GetRequiredService<IStorageDriver>();
        AudioFingerprinter = serviceProvider.GetRequiredService<IAudioFingerprinter>();
        _musicLogicLogger = serviceProvider.GetRequiredService<ILogger<MusicLogic>>();
    }

    public async Task Handle()
    {
        if (Folder is null)
            return;
        if (Library is null)
            return;

        await using MediaScan mediaScan = new(storageDriver);
        IEnumerable<MediaFolderExtend> mediaFolder = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .Process(Folder, 20);

        foreach (MediaFolderExtend list in mediaFolder)
        {
            Log.LogInformation("Music {Path}: Processing", list.Path);

            MusicLogic music = new(
                _musicLogicLogger,
                Library,
                list,
                _mediaContext,
                StorageFactory,
                AudioFingerprinter
            );
            await music.Process();
        }
    }

    public void Dispose()
    {
        _mediaContext.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _mediaContext.DisposeAsync();
    }
}
