using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class FolderRepository(MediaContext context)
{
    public async Task<Folder?> GetFolderByIdAsync(Ulid folderId)
    {
        return await context.Folders.Where(folder => folder.Id == folderId)
            .Include(folder => folder.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Library)
            .FirstOrDefaultAsync();
    }
    
    public Task<Folder?> GetFolderByPathAsync(string requestPath)
    {
        return context.Folders.FirstOrDefaultAsync(folder => folder.Path == requestPath);
    }

    public Task<List<Folder>> GetFoldersByLibraryIdAsync(FolderLibraryDto[] folderLibraries)
    {
        return context.Folders
            .Where(folder => folderLibraries.Select(f => f.FolderId).Contains(folder.Id))
            .ToListAsync();
    }

    public Task GetFolderLibraryByIdAsync(int id, FolderLibrary folderLibrary)
    {
        throw new NotImplementedException();
    }

    public Task<List<Folder>> GetFoldersByLibraryIdAsync(Ulid libraryId)
    {
        return context.FolderLibrary
            .Where(fl => fl.LibraryId == libraryId)
            .Select(fl => fl.Folder)
            .ToListAsync();
    }

    public Task<Folder?> GetFolderById(Ulid folderId)
    {
        return context.Folders
            .Where(folder => folder.Id == folderId)
            .FirstOrDefaultAsync();
    }

    public Task<Folder?> GetFolderByPath(string path)
    {
        return context.Folders
            .FirstOrDefaultAsync(folder => folder.Path == path);
    }

    public Task AddFolderAsync(Folder folder)
    {
        return context.Folders.Upsert(folder)
            .On(f => new { f.Path })
            .WhenMatched((fs, fi) => new()
            {
                Path = fi.Path
            })
            .RunAsync();
    }

    public Task AddFolderLibraryAsync(FolderLibrary folderLibrary)
    {
        return context.FolderLibrary.Upsert(folderLibrary)
            .On(fl => new { fl.LibraryId, fl.FolderId })
            .WhenMatched((fls, fli) => new()
            {
                LibraryId = fli.LibraryId,
                FolderId = fli.FolderId
            })
            .RunAsync();
    }

    public Task AddFolderLibraryAsync(FolderLibrary[] folderLibraries)
    {
        return context.FolderLibrary.UpsertRange(folderLibraries)
            .On(fl => new { fl.LibraryId, fl.FolderId })
            .WhenMatched((fls, fli) => new()
            {
                LibraryId = fli.LibraryId,
                FolderId = fli.FolderId
            })
            .RunAsync();
    }

    public Task UpdateFolderAsync(Folder folder)
    {
        context.Folders.Update(folder);
        return context.SaveChangesAsync();
    }

    public Task DeleteFolderAsync(Folder folder)
    {
        context.Folders.Remove(folder);
        return context.SaveChangesAsync();
    }
}