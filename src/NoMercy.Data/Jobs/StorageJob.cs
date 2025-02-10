using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers.Monitoring;
using NoMercy.Queue;

namespace NoMercy.Data.Jobs;

[Serializable]
public class StorageJob : IShouldQueue
{
    public List<StorageDto> Storage { get; set; } = [];
    
    public StorageJob()
    {
        //
    }
    
    public StorageJob(List<StorageDto> storage)
    {
        Storage = storage;
    }

    public async Task Handle()
    {
        await using MediaContext context = new();
        
        List<Library> libraries = await context.Libraries
            .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
            
            .Include(library => library.LibraryTvs)
                .ThenInclude(folder => folder.Tv)
                .ThenInclude(tv => tv.Episodes)
                .ThenInclude(episode => episode.VideoFiles)
                .ThenInclude(file => file.Metadata)
            
            .Include(folder => folder.LibraryMovies)
                .ThenInclude(folder => folder.Movie)
                .ThenInclude(movie => movie.VideoFiles)
                .ThenInclude(file => file.Metadata)
            
            .Include(folder => folder.AlbumLibraries)
                .ThenInclude(folder => folder.Album)
                .ThenInclude(file => file.Metadata)
            
            .ToListAsync();

        await Parallel.ForEachAsync(libraries, (library, _) =>
        {
            List<Metadata?> movieMetaData = library.LibraryMovies
                .Select(l => l.Movie)
                .SelectMany(m => m.VideoFiles)
                .Where(m => m.Metadata is not null)
                .Select(vf => vf.Metadata)
                .ToList();
            
            List<Metadata?> tvMetaData = library.LibraryTvs
                .Select(l => l.Tv)
                .SelectMany(t => t.Episodes)
                .SelectMany(e => e.VideoFiles)
                .Where(m => m.Metadata is not null)
                .Select(vf => vf.Metadata)
                .ToList();
            
            List<Metadata?> albumMetaData = library.AlbumLibraries
                .Select(l => l.Album)
                .Where(m => m.Metadata is not null)
                .Select(vf => vf.Metadata)
                .ToList();

            foreach (FolderLibrary folderLibraries in library.FolderLibraries)
            {
                StorageDto? storage = Storage.Find(s => s.Path == folderLibraries.Folder.Path);
                
                if (storage?.Data is null) return default;
                
                if (movieMetaData.Count > 0)
                {
                    foreach (Metadata? metadata in movieMetaData.Where(metadata => (bool)metadata?.HostFolder.StartsWith(folderLibraries.Folder.Path.Replace("\\", "/"))))
                    {
                        storage.Data.Movies += metadata?.MovieSize ?? 0;
                        storage.Data.Other += metadata?.OtherSize ?? 0;
                        storage.Data.Used += metadata?.FolderSize ?? 0;
                    }
                }
            
                if (tvMetaData.Count > 0)
                {
                    foreach (Metadata? metadata in tvMetaData.Where(metadata => (bool)metadata?.HostFolder.StartsWith(folderLibraries.Folder.Path.Replace("\\", "/"))))
                    {
                        storage.Data.Shows += metadata?.TvSize ?? 0;
                        storage.Data.Other += metadata?.OtherSize ?? 0;
                        storage.Data.Used += metadata?.FolderSize ?? 0;
                    }
                }
            
                if (albumMetaData.Count > 0)
                {
                    foreach (Metadata? metadata in albumMetaData.Where(metadata => (bool)metadata?.HostFolder.StartsWith(folderLibraries.Folder.Path.Replace("\\", "/"))))
                    {
                        storage.Data.Music += metadata?.MusicSize ?? 0;
                        storage.Data.Other += metadata?.OtherSize ?? 0;
                        storage.Data.Used += metadata?.FolderSize ?? 0;
                    }
                }
            }
            
            
            return default;
        });
    }

    private static long GetDirectorySize(DirectoryInfo directoryInfo)
    {
        if (!directoryInfo.Exists)
        {
            return 0;
        }

        FileInfo[] dirs = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
            
        long totalSize = dirs.Sum(file => file.Length);

        return totalSize;
    }
    
    private static async Task CountFolder(List<string> folders, string library, StorageDto storage, CancellationToken ct = default)
    {
        
        await Parallel.ForEachAsync(folders, ct,(folder, _) =>
        {
            long size = GetDirectorySize(new(folder));

            switch (library)
            {
                case "movie":
                    storage.Data.Movies += size;
                    break;
                case "tv":
                    storage.Data.Shows += size;
                    break;
                case "music":
                    storage.Data.Music += size;
                    break;
            }

            return default;
        });
    }

}