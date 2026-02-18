using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Images;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Music;
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
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view albums");

        string language = Language();

        List<AlbumCardDto> albumCards = await _musicRepository.GetAlbumCardsAsync(userId, letter, language);

        if (albumCards.Count == 0)
            return NotFoundResponse("Albums not found");

        ComponentEnvelope response = Component.Grid()
            .WithItems(albumCards.Select(a => Component.MusicCard(new AlbumsResponseItemDto(a))));

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

        await _eventBus.PublishAsync(new MusicItemLikedEvent
        {
            UserId = User.UserId(),
            ItemId = album.Id,
            ItemType = "album",
            Liked = request.Value
        });

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