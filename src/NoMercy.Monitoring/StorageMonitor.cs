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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Monitoring;

public class StorageMonitor
{
    private static List<Library> _libraries = [];

    public static List<StorageDto> Storage = [];

    public static List<ResourceMonitorDto> Main()
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        List<ResourceMonitorDto> resourceMonitorDtos = [];

        foreach (DriveInfo d in allDrives)
        {
            ResourceMonitorDto resourceMonitorDto = new()
            {
                Name = d.Name,
                Type = d.DriveType.ToString(),
            };
            if (d.IsReady)
            {
                resourceMonitorDto.Total = d.TotalSize / 1024 / 1024 / 1024;
                resourceMonitorDto.Available = d.AvailableFreeSpace / 1024 / 1024 / 1024;
            }

            resourceMonitorDtos.Add(item: resourceMonitorDto);
        }

        return resourceMonitorDtos;
    }

    public static Task UpdateStorage()
    {
        using MediaContext context = new();

        _libraries = context
            .Libraries.Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .Include(navigationPropertyPath: library => library.LibraryTvs)
                .ThenInclude(navigationPropertyPath: folder => folder.Tv)
                    .ThenInclude(navigationPropertyPath: tv => tv.Episodes)
                        .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
            .Include(navigationPropertyPath: folder => folder.LibraryMovies)
                .ThenInclude(navigationPropertyPath: folder => folder.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.VideoFiles)
            .Include(navigationPropertyPath: folder => folder.LibraryTracks)
                .ThenInclude(navigationPropertyPath: folder => folder.Track)
            .ToList();

        foreach (Library library in _libraries)
        foreach (FolderLibrary folderLibrary in library.FolderLibraries)
        {
            StorageDto movieStorageDto = new()
            {
                Path = folderLibrary.Folder.Path,
                Data = new()
                {
                    Movies = 0,
                    Shows = 0,
                    Music = 0,
                    // Free = StorageHelper.GetFreeSpace(folderLibrary.Folder.Path),
                    Used = 0,
                    Other = 0,
                },
            };
            Storage.Add(item: movieStorageDto);

            StorageDto tvStorageDto = new()
            {
                Path = folderLibrary.Folder.Path,
                Data = new()
                {
                    Movies = 0,
                    Shows = 0,
                    Music = 0,
                    // Free = StorageHelper.GetFreeSpace(folderLibrary.Folder.Path),
                    Used = 0,
                    Other = 0,
                },
            };
            Storage.Add(item: tvStorageDto);

            StorageDto musicStorageDto = new()
            {
                Path = folderLibrary.Folder.Path,
                Data = new()
                {
                    Movies = 0,
                    Shows = 0,
                    Music = 0,
                    // Free = StorageHelper.GetFreeSpace(folderLibrary.Folder.Path),
                    Used = 0,
                    Other = 0,
                },
            };
            Storage.Add(item: musicStorageDto);
        }

        Storage = Storage.GroupBy(keySelector: f => f.Path).Select(selector: f => f.First()).ToList();

        return Task.CompletedTask;
    }
}

public record StorageDto
{
    [JsonProperty(propertyName: "path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty(propertyName: "data")]
    public Usage Data { get; set; } = new();
}

public class Usage
{
    [JsonProperty(propertyName: "movies")]
    public long Movies
    {
        get => CalculatePercentage(value: field);
        set => field = value / 1024 / 8;
    }

    [JsonProperty(propertyName: "shows")]
    public long Shows
    {
        get => CalculatePercentage(value: field);
        set => field = value / 1024 / 8;
    }

    [JsonProperty(propertyName: "music")]
    public long Music
    {
        get => CalculatePercentage(value: field);
        set => field = value / 1024 / 8;
    }

    [JsonProperty(propertyName: "other")]
    public long Other
    {
        get => CalculatePercentage(value: field);
        set => field = value / 1024 / 8;
    }

    [JsonProperty(propertyName: "used")]
    public long Used
    {
        get;
        set => field = value / 1024 / 8;
    }

    private long CalculatePercentage(long value)
    {
        if (Used == 0)
            return 0;

        double fraction = (double)value / Used;
        long percentage = (long)(fraction * 100);

        return percentage;
    }
}
