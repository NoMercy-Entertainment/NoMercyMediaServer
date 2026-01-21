using System.Drawing.Imaging;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
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
using NoMercy.Providers.Helpers;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music Artists")]
[Authorize]
[Route("api/v{version:apiVersion}/music/artist")]
public class ArtistsController : BaseController
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public ArtistsController(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    public static event EventHandler<MusicLikeEventDto>? OnLikeEvent;

    [HttpGet]
    [Route("/api/v{version:apiVersion}/music/artists/{letter}")]
    public async Task<IActionResult> Index(string letter)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view artists");

        List<ArtistsResponseItemDto> artists = [];

        foreach (Artist artist in _musicRepository.GetArtistsAsync(userId, letter))
            artists.Add(new(artist));

        List<ArtistTrack> tracks = await _musicRepository.GetArtistTracksForCollectionAsync(
            artists.Select(a => a.Id).ToList());

        foreach (ArtistsResponseItemDto artist in artists)
            artist.Tracks = tracks.Count(track => track.ArtistId == artist.Id);
        
        ComponentEnvelope response = Component.Grid()
            .WithItems(artists
                .Where(response => response.Tracks > 0)
                .OrderBy(artist => artist.Name)
                .Select(item => Component.MusicCard(new()
                {
                    Id = item.Id.ToString(),
                    Name = item.Name,
                    Cover = item.Cover,
                    Type = "artist",
                    Link = $"/music/artist/{item.Id}",
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
            return UnauthorizedResponse("You do not have permission to view artists");

        Artist? artist = await _musicRepository.GetArtistAsync(userId, id);

        string country = Country();

        if (artist is null)
            return NotFoundResponse("Artist not found");

        return Ok(new ArtistResponseDto
        {
            Data = new(artist, userId, country)
        });
    }

    [HttpPost]
    [Route("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like artists");

        await using MediaContext mediaContext = new();
        Artist? artist = await mediaContext.Artists
            .AsNoTracking()
            .Where(artistUser => artistUser.Id == id)
            .FirstOrDefaultAsync();

        if (artist is null)
            return UnprocessableEntityResponse("Artist not found");

        await _musicRepository.LikeArtistAsync(userId, artist, request.Value);

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "artist", artist.Id]
        });

        MusicLikeEventDto musicLikeEventDto = new()
        {
            Id = artist.Id,
            Type = "artist",
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
                artist.Name,
                request.Value ? "liked" : "unliked"
            }
        });
    }

    [HttpPost]
    [Route("{id:guid}/rescan")]
    public async Task<IActionResult> Like(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan artists");

        await using MediaContext mediaContext = new();

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

        Artist? artist = await mediaContext.Artists
            .Include(artist => artist.LibraryFolder)
            .FirstOrDefaultAsync(artist => artist.Id == id);

        if (artist is null)
            return NotFoundResponse("Artist not found");

        string libraryRootFolder = artist.LibraryFolder.Path;
        if (string.IsNullOrEmpty(libraryRootFolder))
            return UnprocessableEntityResponse("Artist library folder not found");

        // save to artist folder
        string filePath = Path.Combine(libraryRootFolder, artist.HostFolder.TrimStart('\\'), artist.Name.ToSlug() + ".jpg");
        Logger.App(filePath);
        await using (FileStream stream = new(filePath, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }

        // save to app images folder
        string filePath2 = Path.Combine(AppFiles.ImagesPath, "music", artist.Name.ToSlug() + ".jpg");
        Logger.App(filePath2);
        await using (FileStream stream = new(filePath2, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }
        
        artist.Cover = $"/{artist.Name.ToSlug()}.jpg";
        artist._colorPalette = await CoverArtImageManagerManager
            .ColorPalette("cover", new(filePath2));
        
        await mediaContext.SaveChangesAsync();
        
        return Ok(new StatusResponseDto<ImageUploadResponseDto>
        {
            Status = "ok",
            Message = "Artist cover updated",
            Data = new()
            {
                Url = new($"/images/music/{artist.Name.ToSlug()}.jpg", UriKind.Relative),
                ColorPalette = artist.ColorPalette
            }
        });
    }
}