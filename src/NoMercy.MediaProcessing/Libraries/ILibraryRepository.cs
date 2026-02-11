using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Libraries;

public interface ILibraryRepository : IDisposable, IAsyncDisposable
{
    public Task<IEnumerable<MediaFolderExtend>> GetRootFoldersAsync(string path);

    public Task<Library?> GetLibraryWithFolders(Ulid id);

    public Task<Folder?> GetLibraryFolder(Ulid folderId);

    public Task<Library?> GetLibraryByIdWithFolders(Ulid libraryId);
}