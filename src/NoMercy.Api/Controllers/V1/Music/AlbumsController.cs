using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.Socket.music;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.MediaProcessing.Images;
using NoMercy.Networking.Dto;
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

    public AlbumsController(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
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

        foreach (Album album in _musicRepository.GetAlbumsAsync(userId, letter))
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
                .Select(item => Component.MusicCard(new()
                {
                    Id = item.Id.ToString(),
                    Name = item.Name,
                    Cover = item.Cover,
                    Type = "artist",
                    Link = $"/music/album/{item.Id}",
                    ColorPalette = null,
                    Disambiguation = item.Disambiguation,
                    Description = item.Description,
                    Tracks = item.Tracks
                })));

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

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
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

    [HttpPost]
    [Route("{id:guid}/cover")]
    public async Task<IActionResult> Cover(Guid id, [FromForm] IFormFile image)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to upload artist covers");

        await using MediaContext mediaContext = new();

        Album? album = await mediaContext.Albums
            .Include(album => album.LibraryFolder)
            .FirstOrDefaultAsync(album => album.Id == id);

        if (album is null)
            return NotFoundResponse("Album not found");

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
        string filePath2 = Path.Combine(AppFiles.ImagesPath, "music", album.Name.ToSlug() + ".jpg");
        Logger.App(filePath2);
        await using (FileStream stream = new(filePath2, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }
        
        album.Cover = $"/{album.Name.ToSlug()}.jpg";
        album._colorPalette = await CoverArtImageManagerManager
            .ColorPalette("cover", new(filePath2));
        
        await mediaContext.SaveChangesAsync();
        
        return Ok(new StatusResponseDto<ImageUploadResponseDto>
        {
            Status = "ok",
            Message = "Album cover updated",
            Data = new()
            {
                Url = new($"/images/music/{album.Name.ToSlug()}.jpg", UriKind.Relative),
                ColorPalette = album.ColorPalette
            }
        });
    }
}