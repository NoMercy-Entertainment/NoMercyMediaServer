using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Logic;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Queue;

namespace NoMercy.Data.Jobs;

[Serializable]
public class FindMediaFilesJob : IShouldQueue
{
    public int Id { get; set; }
    public Library? Library { get; set; }

    public FindMediaFilesJob()
    {
        //
    }

    public FindMediaFilesJob(int id, Library library)
    {
        Id = id;
        Library = library;
    }

    public async Task Handle()
    {
        if (Library == null) return;

        Logger.Queue($"Finding media files for {Id} in library {Library.Id.ToString()}");

        await using MediaContext context = new();
        Library? library = await context.Libraries
            .AsTracking()
            .Where(f => f.Id == Library.Id)
            .Include(l => l.FolderLibraries)
            .ThenInclude(fl => fl.Folder)
            .Include(l => l.LibraryMovies
                .Where(lm => lm.Movie.Id == Id)
            )
            .ThenInclude(lm => lm.Movie)
            .Include(l => l.LibraryTvs
                .Where(lt => lt.Tv.Id == Id)
            )
            .ThenInclude(lt => lt.Tv)
            .FirstOrDefaultAsync();

        if (library == null) return;

        await using FileLogic file = new(Id, library);
        await file.Process();

        if (file.Files.Count > 0)
        {
            Logger.App($"Found {file.Files.Count} files in {file.Files.FirstOrDefault()?.Path}");

            if (library.LibraryMovies.Count > 0)
            {
                LibraryMovie? libraryMovie = library.LibraryMovies.FirstOrDefault();
                if (libraryMovie == null) return;

                libraryMovie.Movie.Folder = file.Files.FirstOrDefault()?.Path;

                await context.SaveChangesAsync();
            }
            else if (library.LibraryTvs.Count > 0)
            {
                LibraryTv? libraryTv = library.LibraryTvs.FirstOrDefault();
                if (libraryTv == null) return;

                libraryTv.Tv.Folder = file.Files.FirstOrDefault()?.Path;

                await context.SaveChangesAsync();
            }

            Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
            {
                QueryKey = ["libraries", library.Id.ToString()]
            });
        }

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = [library.Type == "movie" ? "movie" : "tv", Id]
        });
    }
}