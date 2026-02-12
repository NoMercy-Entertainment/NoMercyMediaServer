using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Libraries;

public interface ILibraryRepository : IDisposable, IAsyncDisposable
{
    public Task<IEnumerable<MediaFolderExtend>> GetRootFoldersAsync(string path);

    public Task<Library?> GetLibraryWithFolders(Ulid id);

    public Task<Folder?> GetLibraryFolder(Ulid folderId);

    public Task<Library?> GetLibraryByIdWithFolders(Ulid libraryId);
}