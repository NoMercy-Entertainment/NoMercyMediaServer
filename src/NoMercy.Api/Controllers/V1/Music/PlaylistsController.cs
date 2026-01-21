using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.MediaProcessing.Images;
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

        List<Playlist> playlists = await _musicRepository.GetPlaylistsAsync(userId);
        
        return Ok(new StatusResponseDto<List<Playlist>>
        {
            Status = "ok", 
            Data = playlists, 
        });
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

        return Ok(new TracksResponseDto
        {
            Data = new()
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover is not null
                    ? new Uri($"/images/music{playlist.Cover}", UriKind.Relative)
                    : null,
                Description = playlist.Description,
                ColorPalette = playlist.ColorPalette,
                Tracks = playlist.Tracks
                    .Select(t => new ArtistTrackDto(t.Track, language))
                    .ToList()
            }
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
        
        // save to app images folder
        string filePath = Path.Combine(AppFiles.ImagesPath, "music", newPlaylist.Name.ToSlug() + ".jpg");
        Logger.App(filePath);
        
        if (request.Cover is not null)
        {
            await using (FileStream stream = new(filePath, FileMode.OpenOrCreate))
            {
                string base64Data = Regex.Match(request.Cover, @"data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;
                byte[] binData = Convert.FromBase64String(base64Data);
                await stream.WriteAsync(binData);
            }
        
            newPlaylist.Cover = $"/{newPlaylist.Name.ToSlug()}.jpg";
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
        
        int result = await _mediaContext.Playlists.ExecuteUpdateAsync(p => p
            .SetProperty(pl => pl.Name, request.Name)
            .SetProperty(pl => pl.Description, request.Description));

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Playlist updated successfully" : "No changes made").Localize(),
            Status = "ok",
        });
    }

    [HttpPost]
    [Route("{id:guid}/add")]
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

        return Ok(new StatusResponseDto<string>
        {
            Data = (result > 0 ? "Playlist deleted successfully" : "Playlist not found").Localize(),
            Status = "ok",
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