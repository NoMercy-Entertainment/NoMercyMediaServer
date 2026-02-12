using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Libraries;

public class LibraryRepository(MediaContext context) : ILibraryRepository
{
    public async Task<IEnumerable<MediaFolderExtend>> GetRootFoldersAsync(string path)
    {
        await using MediaScan mediaScan = new();
        return (await mediaScan
                .DisableRegexFilter()
                .Process(path, 2))
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();
    }

    public Task<Library?> GetLibraryWithFolders(Ulid id)
    {
        return context.Libraries
            .AsNoTracking()
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(library => library.Id == id);
    }

    public Task<Folder?> GetLibraryFolder(Ulid folderId)
    {
        return context.Folders
            .Include(folder => folder.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Library)
            .ThenInclude(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .Include(folder => folder.EncoderProfileFolder)
            .ThenInclude(encoderProfileFolder => encoderProfileFolder.EncoderProfile)
            .FirstOrDefaultAsync(folder => folder.Id == folderId);
    }

    public Task<Library?> GetLibraryByIdWithFolders(Ulid libraryId)
    {
        return context.Libraries
            .AsNoTracking()
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(library => library.Id == libraryId);
    }

    public void Dispose()
    {
        context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
    }
}