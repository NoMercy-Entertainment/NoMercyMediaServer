using NoMercy.Database.Models.Libraries;

namespace NoMercy.Data.Repositories;

public interface IFolderRepository
{
    Task<Folder?> GetFolderByIdAsync(Ulid folderId);

    Task<Folder?> GetFolderByPathAsync(string requestPath);

    Task<Folder?> GetFolderByDriverAndPathAsync(Ulid driverId, string requestPath);

    Task<List<Folder>> GetFoldersByLibraryIdAsync(FolderLibraryDto[] folderLibraries);

    Task<int> GetFolderLibraryByIdAsync(int id, FolderLibrary folderLibrary);

    Task<List<Folder>> GetFoldersByLibraryIdAsync(Ulid libraryId);

    Task<Folder?> GetFolderById(Ulid folderId);

    Task<Folder?> GetFolderByPath(string path);

    Task<int> AddFolderAsync(Folder folder);

    Task<int> AddFolderLibraryAsync(FolderLibrary folderLibrary);

    Task<int> AddFolderLibraryAsync(FolderLibrary[] folderLibraries);

    Task<int> UpdateFolderAsync(Folder folder);

    Task<int> DeleteFolderAsync(Folder folder);

    Task<int> SyncFolderLibraryAsync(FolderLibrary[] folderLibraries, List<Folder> folders);
}
