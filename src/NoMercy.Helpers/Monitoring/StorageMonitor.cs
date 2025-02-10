using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Helpers.Monitoring;

public class StorageMonitor
{
    public static List<Library> Libraries { get; set; } = [];
    
    public static List<StorageDto> Storage { get; set; } = [];
    
    public static List<ResourceMonitorDto> Main()
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        List<ResourceMonitorDto> resourceMonitorDtos = [];

        foreach (DriveInfo d in allDrives)
        {
            ResourceMonitorDto resourceMonitorDto = new()
            {
                Name = d.Name,
                Type = d.DriveType.ToString()
            };
            if (d.IsReady)
            {
                resourceMonitorDto.Total = d.TotalSize / 1024 / 1024 / 1024;
                resourceMonitorDto.Available = d.AvailableFreeSpace / 1024 / 1024 / 1024;
            }

            resourceMonitorDtos.Add(resourceMonitorDto);
        }

        return resourceMonitorDtos;
    }
    
    public static Task UpdateStorage()
    {
        using MediaContext context = new();
        
        Libraries = context.Libraries
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(library => library.LibraryTvs)
            .ThenInclude(folder => folder.Tv)
            .ThenInclude(tv => tv.Episodes)
            .ThenInclude(episode => episode.VideoFiles)
            .Include(folder => folder.LibraryMovies)
            .ThenInclude(folder => folder.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .Include(folder => folder.LibraryTracks)
            .ThenInclude(folder => folder.Track)
            .ToList();
        
        foreach (Library library in Libraries)
        {
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
                        Other = 0
                    }
                };
                Storage.Add(movieStorageDto);
                
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
                        Other = 0
                    }
                };
                Storage.Add(tvStorageDto);
                
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
                        Other = 0
                    }
                };
                Storage.Add(musicStorageDto);
            }
        }
        
        Storage = Storage.GroupBy(f => f.Path)
            .Select(f => f.First())
            .ToList();
        
        return Task.CompletedTask;
    }
}

public record StorageDto
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
    [JsonProperty("data")] public Usage Data { get; set; } = new();
}

public class Usage
{
    private long _movies;
    private long _shows;
    private long _music;
    private long _other;
    private long _free;
    private long _used;

    [JsonProperty("movies")]
    public long Movies
    {
        get => CalculatePercentage(_movies);
        set => _movies = value / 1024 / 8;
    }

    [JsonProperty("shows")]
    public long Shows
    {
        get => CalculatePercentage(_shows);
        set => _shows = value / 1024 / 8;
    }

    [JsonProperty("music")]
    public long Music
    {
        get => CalculatePercentage(_music);
        set => _music = value / 1024 / 8;
    }

    [JsonProperty("other")]
    public long Other
    {
        get => CalculatePercentage(_other);
        set => _other = value / 1024 / 8;
    }

    [JsonProperty("used")]
    public long Used
    {
        get => _used;
        set => _used = value / 1024 / 8;
    }

    [JsonProperty("free")]
    // public long Free => 0;
    // {
    //     get
    //     {
    //         if (Used > 0)
    //         {
    //             return (Used + _free) > 0 ? (_free / (Used + _free) * 100) : 0;
    //         }
    //         return _free;
    //     }
    //     set => _free = (long)value;
    // }

    [JsonIgnore]
    private long Total => (Used + _free);

    private long CalculatePercentage(long value)
    {
        if (Used == 0) return 0;

        double fraction = (double)value / Used;
        long percentage = (long)(fraction * 100);

        return percentage;
    }
}
