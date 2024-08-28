using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;

namespace NoMercy.MediaProcessing.Libraries;

public class LibraryRepository(MediaContext context) : ILibraryRepository
{
    public async Task<IEnumerable<MediaFolder>> GetRootFoldersAsync(string path)
    {
        await using MediaScan mediaScan = new();
        return (await mediaScan
                .DisableRegexFilter()
                .Process(path, 2))
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();
    }

    public Task<Library?> GetLibraryWithFoldersAsync(Ulid id)
    {
        return context.Libraries
            .AsNoTracking()
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(library => library.Id == id);
    }

    public void Dispose()
    {
        context.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}