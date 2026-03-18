using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Services;

public class SetupService
{
    private readonly MediaContext _mediaContext;
    private readonly LibraryRepository _libraryRepository;
    private readonly HomeRepository _homeRepository;

    public SetupService(HomeRepository homeRepository, LibraryRepository libraryRepository, MediaContext mediaContext)
    {
        _homeRepository = homeRepository;
        _libraryRepository = libraryRepository;
        _mediaContext = mediaContext;
    }
    
    public Task<List<Library>> GetSetupLibraries(Guid userId)
    {
        return _mediaContext.Libraries
            .AsNoTracking()
            .Where(library => library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(library => library.FolderLibraries)
            .ThenInclude(fl => fl.Folder)
            .ThenInclude(f => f.EncoderProfileFolder)
            .ThenInclude(epf => epf.EncoderProfile)
            .Include(library => library.LanguageLibraries)
            .ThenInclude(ll => ll.Language)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .OrderBy(library => library.Order)
            .ToListAsync();
    }

    public Task<List<Playlist>> GetSetupPlaylistsAsync(Guid userId)
    {
        return _mediaContext.Playlists
            .AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .ToListAsync();
    }
    

    public async Task<ScreensaverDto> GetSetupScreensaverContent(Guid userId)
    {
        HashSet<Image> data = await _homeRepository.GetScreensaverImagesAsync(_mediaContext, userId);

        IEnumerable<Image> logos = data.Where(image => image.Type == "logo");

        IEnumerable<ScreensaverDataDto> tvCollection = data
            .Where(image => image is { TvId: not null, Type: "backdrop" })
            .DistinctBy(image => image.TvId)
            .Select(image => new ScreensaverDataDto(image, logos, Config.TvMediaType));

        IEnumerable<ScreensaverDataDto> movieCollection = data
            .Where(image => image is { MovieId: not null, Type: "backdrop" })
            .DistinctBy(image => image.MovieId)
            .Select(image => new ScreensaverDataDto(image, logos, Config.MovieMediaType));

        return new()
        {
            Data = tvCollection
                .Concat(movieCollection)
                .Where(image => image.Meta?.Logo != null)
                .Randomize()
        };
    }

}