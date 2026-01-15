namespace NoMercy.MediaProcessing.Libraries;

public interface ILibraryManager : IDisposable, IAsyncDisposable
{
    public Task ProcessLibrary(Ulid id);
}