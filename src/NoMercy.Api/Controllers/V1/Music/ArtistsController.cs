using System.Text.RegularExpressions;
using Asp.Versioning;
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
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

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

        foreach (Artist artist in _musicRepository.GetArtists(userId, letter))
            artists.Add(new(artist));

        List<ArtistTrack> tracks = await _musicRepository.GetArtistTracksForCollectionAsync(
            artists.Select(a => a.Id).ToList());

        foreach (ArtistsResponseItemDto artist in artists)
            artist.Tracks = tracks.Count(track => track.ArtistId == artist.Id);
        
        ComponentEnvelope response = Component.Grid()
            .WithItems(artists
                .Where(response => response.Tracks > 0)
                .OrderBy(artist => artist.Name)
                .Select(item => Component.MusicCard(item)));
                // {
                //     Id = item.Id.ToString(),
                //     Name = item.Name,
                //     Cover = item.Cover,
                //     Type = "artist",
                //     Link = $"/music/artist/{item.Id}",
                //     ColorPalette = null,
                //     Disambiguation = item.Disambiguation,
                //     Description = item.Description,
                //     Tracks = item.Tracks
                // })));

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

        Artist? artist = await _mediaContext.Artists
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

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Rescan started",
            Args = []
        });
    }
    
    [HttpDelete]
    [Route("{id:guid}")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete an artist");
        
        int result = await _mediaContext.Artists
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync();
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "artist"]
        });

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Artist deleted successfully" : "Artist not found").Localize(),
            Status = "ok",
        });
    }

    [HttpPatch]
    [Route("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] UpdateMusicMetadataRequestDto request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to edit an artist");
        
        Artist? artist = await _mediaContext.Artists
            .FirstOrDefaultAsync(a => a.Id == id);
        
        if (artist is null)
            return NotFoundResponse("Artist not found");
        
        string slug = artist.Name.ToSlug();
        string colorPalette = artist._colorPalette;
        string cover = artist.Cover ?? "";
        
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
        
        artist.Name = request.Name ?? artist.Name;
        artist.Description = request.Description;
        artist.Cover = cover;
        artist._colorPalette = colorPalette;

        int result = await _mediaContext.SaveChangesAsync();
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "artist", id]
        });

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Artist updated successfully" : "No changes made").Localize(),
            Status = "ok",
        });
    }

    [HttpPost]
    [Route("{id:guid}/cover")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Cover(Guid id, IFormFile image)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to upload artist covers");

        Artist? artist = await _mediaContext.Artists
            .Include(artist => artist.LibraryFolder)
            .FirstOrDefaultAsync(artist => artist.Id == id);

        if (artist is null)
            return NotFoundResponse("Artist not found");

        string slug = artist.Name.ToSlug();

        string libraryRootFolder = artist.LibraryFolder.Path;
        if (string.IsNullOrEmpty(libraryRootFolder))
            return UnprocessableEntityResponse("Artist library folder not found");

        // save to artist folder
        string filePath = Path.Combine(libraryRootFolder, artist.HostFolder.TrimStart('\\'), slug + ".jpg");
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

        artist.Cover = $"/{slug}.jpg";
        artist._colorPalette = await CoverArtImageManagerManager
            .ColorPalette("cover", new(filePath2));

        await _mediaContext.SaveChangesAsync();
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "artist", artist.Id]
        });
        
        return Ok(new StatusResponseDto<ImageUploadResponseDto>
        {
            Status = "ok",
            Message = "Artist cover updated",
            Data = new()
            {
                Url = new($"/images/music/{slug}.jpg", UriKind.Relative),
                ColorPalette = artist.ColorPalette
            }
        });
    }
}