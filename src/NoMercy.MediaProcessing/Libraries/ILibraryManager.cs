namespace NoMercy.MediaProcessing.Libraries;

public interface ILibraryManager : IDisposable, IAsyncDisposable
{
    public Task ProcessLibrary(Ulid id);
    public Task ProcessNewLibraryItems(Ulid id);
}