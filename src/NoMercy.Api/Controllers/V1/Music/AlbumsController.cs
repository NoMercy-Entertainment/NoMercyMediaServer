using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Services.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Images;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[Tags("Music Albums")]
[Authorize]
[Route("api/v{version:apiVersion}/music/album")]
public class AlbumsController : BaseController
{
    public static event EventHandler<MusicLikeEventDto>? OnLikeEvent;
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;
    private readonly IEventBus _eventBus;

    public AlbumsController(MusicRepository musicService, MediaContext mediaContext, IEventBus eventBus)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
        _eventBus = eventBus;
    }

    [HttpGet]
    [Route("/api/v{version:apiVersion}/music/albums/{letter}")]
    public async Task<IActionResult> Index(string letter)
    {
        List<AlbumsResponseItemDto> albums = [];
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view albums");

        string language = Language();

        foreach (Album album in _musicRepository.GetAlbums(userId, letter))
        {
            albums.Add(new(album, language));
        }

        List<AlbumTrack> tracks = await _musicRepository.GetAlbumTracksForIdsAsync(
            albums.Select(a => a.Id).ToList());

        if (tracks.Count == 0)
            return NotFoundResponse("Albums not found");

        foreach (AlbumsResponseItemDto album in albums)
        {
            album.Tracks = tracks.Count(track => track.AlbumId == album.Id);
        }
        
        ComponentEnvelope response = Component.Grid()
            .WithItems(albums
                .Where(response => response.Tracks > 0)
                .OrderBy(album => album.Name)
                .Select(Component.MusicCard));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view albums");

        string language = Language();

        Album? album = await _musicRepository.GetAlbumAsync(userId, id);

        if (album is null)
            return NotFoundResponse("Albums not found");

        return Ok(new AlbumResponseDto
        {
            Data = new(album, language)
        });
    }

    [HttpPost]
    [Route("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like albums");

        Album? album = await _musicRepository.GetAlbumAsync(userId, id);

        if (album is null)
            return UnprocessableEntityResponse("Albums not found");

        await _musicRepository.LikeAlbumAsync(userId, album, request.Value);

        await _eventBus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "album", album.Id]
        });

        MusicLikeEventDto musicLikeEventDto = new()
        {
            Id = album.Id,
            Type = "album",
            Liked = request.Value,
            User = User.User()
        };

        OnLikeEvent?.Invoke(this, musicLikeEventDto);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{0} {1}",
            Args = new object[]
            {
                album.Name,
                request.Value ? "liked" : "unliked"
            }
        });
    }

    [HttpPost]
    [Route("{id:guid}/rescan")]
    public IActionResult Rescan(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan albums");

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Rescan started",
            Args = []
        });
    }
    
    
    [HttpPatch]
    [Route("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] CreatePlaylistRequestDto request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to edit an album");
        
        Album? album = await _mediaContext.Albums
            .FirstOrDefaultAsync(a => a.Id == id);
        
        if (album is null)
            return NotFoundResponse("Album not found");
        
        string slug = album.Name.ToSlug();
        string colorPalette = album._colorPalette;
        string cover = album.Cover ?? "";
        
        if (request.Cover is not null)
        {
            cover = $"/{slug}.jpg";
            string filePath = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");
            
            await using (FileStream stream = new(filePath, FileMode.Create))
            {
                string base64Data = Regex.Match(request.Cover, "data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;
                byte[] binData = Convert.FromBase64String(base64Data);
                await stream.WriteAsync(binData);
            }
            
            colorPalette = await CoverArtImageManagerManager.ColorPalette("cover", new(filePath));
        }
        
        album.Name = request.Name;
        album.Description = request.Description;
        album.Cover = cover;
        album._colorPalette = colorPalette;

        int result = await _mediaContext.SaveChangesAsync();

        await _eventBus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "album", id]
        });

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Album updated successfully" : "No changes made").Localize(),
            Status = "ok",
        });
    }
    
    [HttpPost]
    [Route("{id:guid}/cover")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Cover(Guid id, IFormFile image)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to upload album covers");

        Album? album = await _mediaContext.Albums
            .Include(album => album.LibraryFolder)
            .FirstOrDefaultAsync(album => album.Id == id);

        if (album is null)
            return NotFoundResponse("Album not found");

        string slug = album.Name.ToSlug();

        string libraryRootFolder = album.LibraryFolder.Path;
        if (string.IsNullOrEmpty(libraryRootFolder))
            return UnprocessableEntityResponse("Album library folder not found");

        // save to album folder
        string filePath = Path.Combine(libraryRootFolder, album.HostFolder.TrimStart('\\'), "cover.jpg");
        Logger.App(filePath);
        await using (FileStream stream = new(filePath, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }

        // save to app images folder
        string filePath2 = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");
        Logger.App(filePath2);
        await using (FileStream stream = new(filePath2, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }

        album.Cover = $"/{slug}.jpg";
        album._colorPalette = await CoverArtImageManagerManager
            .ColorPalette("cover", new(filePath2));

        await _mediaContext.SaveChangesAsync();
        
        return Ok(new StatusResponseDto<ImageUploadResponseDto>
        {
            Status = "ok",
            Message = "Album cover updated",
            Data = new()
            {
                Url = new($"/images/music/{slug}.jpg", UriKind.Relative),
                ColorPalette = album.ColorPalette
            }
        });
    }
}