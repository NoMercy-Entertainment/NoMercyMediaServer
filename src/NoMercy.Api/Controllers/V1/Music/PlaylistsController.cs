using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.MediaProcessing.Images;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using CarouselResponseItemDto = NoMercy.Data.Repositories.CarouselResponseItemDto;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music Playlists")]
[Authorize]
[Route("api/v{version:apiVersion}/music/playlists", Order = 3)]
public class PlaylistsController : BaseController
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public PlaylistsController(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        List<CarouselResponseItemDto> playlists = [];
        playlists.AddRange(await _musicRepository.GetCarouselPlaylistsAsync(userId));

        List<MusicCardData> musicCards = playlists
            .Select(p => new MusicCardData(p))
            .ToList();

        ComponentEnvelope response = Component.Grid()
            .WithItems(musicCards.Select(Component.MusicCard));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view playlists");

        Playlist? playlist = await _musicRepository.GetPlaylistAsync(userId, id);

        if (playlist == null)
            return NotFoundResponse("Playlist not found");

        string language = Language();
        
        return Ok(new PlaylistResponseDto
        {
            Data = new(playlist, language)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlaylistRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to create a playlist");
        
        if(await _mediaContext.Playlists.AnyAsync(p => p.Name == request.Name && p.UserId == User.UserId()))
            return ConflictResponse("You already have a playlist with that name");
        
        Playlist newPlaylist = new()
        {
            Name = request.Name,
            Description = request.Description,
            UserId = User.UserId()
        };

        string slug = newPlaylist.Name.ToSlug();
        
        // save to app images folder
        string filePath = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");
        Logger.App(filePath);
        
        if (request.Cover is not null)
        {
            await using (FileStream stream = new(filePath, FileMode.OpenOrCreate))
            {
                string base64Data = Regex.Match(request.Cover, "data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;
                byte[] binData = Convert.FromBase64String(base64Data);
                await stream.WriteAsync(binData);
            }
        
            newPlaylist.Cover = $"/{slug}.jpg";
            newPlaylist._colorPalette = await CoverArtImageManagerManager
                .ColorPalette("cover", new(filePath));
        }
        
        Logger.App(newPlaylist);
        _mediaContext.Playlists.Add(newPlaylist);
        
        if (request.Tracks.Count > 0)
        {
            foreach (Guid trackId in request.Tracks)
            {
                PlaylistTrack playlistTrack = new()
                {
                    PlaylistId = newPlaylist.Id,
                    TrackId = trackId,
                };
        
                _mediaContext.PlaylistTrack.Add(playlistTrack);
            }
        
        }
        
        await _mediaContext.SaveChangesAsync();
        
        Playlist playlist = _mediaContext.Playlists
            .Include(p => p.Tracks)
            .ThenInclude(pt => pt.Track)
            .First(p => p.Name == request.Name && p.UserId == User.UserId());

        return Ok(new StatusResponseDto<Playlist>()
        {
            Data = playlist,
            Status = "ok",
        });
    }

    [HttpPatch]
    [Route("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] CreatePlaylistRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to edit a playlist");
        
        int result = await _mediaContext.Playlists
            .Where(p => p.Id == id && p.UserId == User.UserId())
            .ExecuteUpdateAsync(p => p
                .SetProperty(pl => pl.Name, request.Name)
                .SetProperty(pl => pl.Description, request.Description));
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "playlists", id]
        });

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Playlist updated successfully" : "No changes made").Localize(),
            Status = "ok",
        });
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to delete a playlist");
        
        int result = await _mediaContext.Playlists
            .Where(p => p.Id == id && p.UserId == User.UserId())
            .ExecuteDeleteAsync();
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music-playlists"]
        });

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Playlist deleted successfully" : "Playlist not found").Localize(),
            Status = "ok",
        });
    }
    
    [HttpPost]
    [Route("{id:guid}/cover")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Cover(Guid id, IFormFile image)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to upload playlist covers");

        await using MediaContext mediaContext = new();

        Playlist? playlist = await mediaContext.Playlists
            .Where(pt => pt.UserId == User.UserId())
            .FirstOrDefaultAsync(playlist => playlist.Id == id);

        if (playlist is null)
            return NotFoundResponse("Playlist not found");

        string slug = playlist.Name.ToSlug();

        // save to app images folder
        string filePath2 = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");
        Logger.App(filePath2);
        await using (FileStream stream = new(filePath2, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }
        
        playlist.Cover = $"/{slug}.jpg";
        playlist._colorPalette = await CoverArtImageManagerManager
            .ColorPalette("cover", new(filePath2));
        
        await mediaContext.SaveChangesAsync();
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "playlists", playlist.Id]
        });
        
        return Ok(new StatusResponseDto<ImageUploadResponseDto>
        {
            Status = "ok",
            Message = "Playlist cover updated",
            Data = new()
            {
                Url = new($"/images/music/{slug}.jpg", UriKind.Relative),
                ColorPalette = playlist.ColorPalette
            }
        });
    }

    [HttpPost]
    [Route("{id:guid}/tracks")]
    public async Task<IActionResult> AddTrack(Guid id, [FromBody] CreatePlaylistTrackRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to edit a playlist");

        PlaylistTrack playlistTrack = new()
        {
            PlaylistId = id,
            TrackId = request.Id,
        };

        _mediaContext.PlaylistTrack.Add(playlistTrack);
        int result = await _mediaContext.SaveChangesAsync();
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "playlists", id]
        });

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Playlist updated successfully" : "No changes made").Localize(),
            Status = "ok",
        });
    }

    [HttpDelete]
    [Route("{id:guid}/tracks/{trackId:guid}")]
    public async Task<IActionResult> AddTrack(Guid id, Guid trackId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to edit a playlist");

        PlaylistTrack? playlistTrack = await _mediaContext.PlaylistTrack
            .Where(pt => pt.Playlist.UserId == User.UserId())
            .FirstOrDefaultAsync(pt => pt.PlaylistId == id && pt.TrackId == trackId);
        
        if (playlistTrack is null)
            return NotFoundResponse("Track not found in playlist");
        
        _mediaContext.PlaylistTrack.Remove(playlistTrack);
        
        int result = await _mediaContext.SaveChangesAsync();
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "playlists", id]
        });

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Playlist updated successfully" : "No changes made").Localize(),
            Status = "ok",
        });
    }
}

public class CreatePlaylistRequestDto
{
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("tracks")] public List<Guid> Tracks { get; set; } = [];
    
}

public class CreatePlaylistTrackRequestDto
{
    [JsonProperty("id")] public Guid Id { get; set; }
}