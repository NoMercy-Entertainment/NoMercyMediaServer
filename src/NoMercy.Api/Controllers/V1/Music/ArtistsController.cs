using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Music;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

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
    private readonly IEventBus _eventBus;
    private readonly IStorageFactory _storageFactory;

    public ArtistsController(
        MusicRepository musicService,
        MediaContext mediaContext,
        IEventBus eventBus,
        IStorageFactory storageFactory
    )
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
        _eventBus = eventBus;
        _storageFactory = storageFactory;
    }

    [HttpGet]
    [Route("/api/v{version:apiVersion}/music/artists/{letter}")]
    public async Task<IActionResult> Index(string letter, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view artists");

        // Lolomo with the "all" marker (`_`) returns one carousel per first-letter
        // bucket in alphabetical order, with the symbol bucket (#) at the end.
        if (request.Version == "lolomo" && (letter == "_" || letter == "all"))
        {
            List<ArtistCardDto> allCards = await _musicRepository.GetAllArtistCardsAsync(userId);

            List<ComponentEnvelope> items = [Component.Container()];

            IOrderedEnumerable<IGrouping<string, ArtistCardDto>> groups = allCards
                .GroupBy(a => BucketLetter(a.Name))
                .OrderBy(g => g.Key == "#" ? "zz" : g.Key);

            foreach (IGrouping<string, ArtistCardDto> group in groups)
            {
                items.Add(
                    Component
                        .Carousel()
                        .WithId($"artists-{group.Key.ToLowerInvariant()}")
                        .WithTitle($"Artists: {group.Key}".Localize())
                        .WithItems(group.Select(a => Component.MusicCard(new MusicCardData(a))))
                );
            }

            return Ok(ComponentResponse.From(items));
        }

        List<ArtistCardDto> artistCards = await _musicRepository.GetArtistCardsAsync(
            userId,
            letter
        );

        string displayLetter = letter == "_" ? "#" : letter.ToUpperInvariant();

        if (request.Version == "lolomo")
        {
            List<ComponentEnvelope> items =
            [
                Component.Container(),
                Component
                    .Carousel()
                    .WithId($"artists-{letter}")
                    .WithTitle($"Artists: {displayLetter}".Localize())
                    .WithItems(artistCards.Select(a => Component.MusicCard(new MusicCardData(a)))),
            ];

            return Ok(ComponentResponse.From(items));
        }

        ComponentEnvelope grid = Component
            .Grid()
            .WithId($"artists-{letter}")
            .WithTitle($"Artists: {displayLetter}".Localize())
            .WithItems(artistCards.Select(a => Component.MusicCard(new MusicCardData(a))));

        return Ok(ComponentResponse.From(grid));
    }

    private static string BucketLetter(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "#";
        char first = char.ToLowerInvariant(name[0]);
        return first >= 'a' && first <= 'z' ? first.ToString().ToUpperInvariant() : "#";
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

        return Ok(new ArtistResponseDto { Data = new(artist, userId, country) });
    }

    [HttpPost]
    [Route("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like artists");

        Artist? artist = await _mediaContext
            .Artists.AsNoTracking()
            .Where(artistUser => artistUser.Id == id)
            .FirstOrDefaultAsync();

        if (artist is null)
            return UnprocessableEntityResponse("Artist not found");

        await _musicRepository.LikeArtistAsync(userId, artist, request.Value);

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent { QueryKey = ["music", "artist", artist.Id] }
        );

        await _eventBus.PublishAsync(
            new MusicItemLikedEvent
            {
                UserId = User.UserId(),
                ItemId = artist.Id,
                ItemType = "artist",
                Liked = request.Value,
            }
        );

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{0} {1}",
                Args = new object[] { artist.Name, request.Value ? "liked" : "unliked" },
            }
        );
    }

    [HttpPost]
    [Route("{id:guid}/rescan")]
    public async Task<IActionResult> Like(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan artists");

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Rescan started",
                Args = [],
            }
        );
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete an artist");

        int result = await _mediaContext.Artists.Where(p => p.Id == id).ExecuteDeleteAsync();

        await _eventBus.PublishAsync(new LibraryRefreshEvent { QueryKey = ["music", "artist"] });

        return Ok(
            new StatusResponseDto<string>
            {
                Data = (result > 0 ? "Artist deleted successfully" : "Artist not found").Localize(),
                Status = "ok",
            }
        );
    }

    [HttpPatch]
    [Route("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] UpdateMusicMetadataRequestDto request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to edit an artist");

        Artist? artist = await _mediaContext.Artists.FirstOrDefaultAsync(a => a.Id == id);

        if (artist is null)
            return NotFoundResponse("Artist not found");

        string slug = artist.Name.ToSlug();
        string colorPalette = artist._colorPalette.OrEmpty();
        string cover = artist.Cover.OrEmpty();

        if (request.Cover is not null)
        {
            Match coverMatch = Regex.Match(request.Cover, "data:image/(?<type>.+?),(?<data>.+)");
            if (!coverMatch.Success)
                return BadRequestResponse("Cover must be a data:image/...;base64,... payload");

            byte[] binData;
            try
            {
                binData = Convert.FromBase64String(coverMatch.Groups["data"].Value);
            }
            catch (FormatException)
            {
                return BadRequestResponse("Cover payload is not valid base64");
            }

            cover = $"/{slug}.jpg";
            string filePath = Path.Combine(AppFiles.ImagesPath, "music", slug + ".jpg");

            await using (FileStream stream = new(filePath, FileMode.Create))
                await stream.WriteAsync(binData);

            colorPalette = await CoverArtImageManagerManager.ColorPalette("cover", new(filePath));
        }

        artist.Name = request.Name ?? artist.Name;
        artist.Description = request.Description;
        artist.Cover = cover;
        artist._colorPalette = colorPalette;

        int result = await _mediaContext.SaveChangesAsync();

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent { QueryKey = ["music", "artist", id] }
        );

        return Ok(
            new StatusResponseDto<string>
            {
                Data = (result > 0 ? "Artist updated successfully" : "No changes made").Localize(),
                Status = "ok",
            }
        );
    }

    [HttpPost]
    [Route("{id:guid}/cover")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Cover(Guid id, IFormFile image)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to upload artist covers");

        Artist? artist = await _mediaContext
            .Artists.Include(artist => artist.LibraryFolder)
                .ThenInclude((Folder folder) => folder.Driver)
            .FirstOrDefaultAsync(artist => artist.Id == id);

        if (artist is null)
            return NotFoundResponse("Artist not found");

        string slug = artist.Name.ToSlug();

        IStorage folderStorage = _storageFactory.For(
            artist.LibraryFolder.Id,
            artist.LibraryFolder.DriverId,
            string.Empty
        );
        string libraryRootFolder = folderStorage.GetFullPath(artist.LibraryFolder.Path);
        if (string.IsNullOrEmpty(libraryRootFolder))
            return UnprocessableEntityResponse("Artist library folder not found");

        // save to artist folder
        string filePath = Path.Combine(
            libraryRootFolder,
            artist.HostFolder.TrimStart('\\'),
            slug + ".jpg"
        );
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
        artist._colorPalette = await CoverArtImageManagerManager.ColorPalette(
            "cover",
            new(filePath2)
        );

        await _mediaContext.SaveChangesAsync();

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent { QueryKey = ["music", "artist", artist.Id] }
        );

        return Ok(
            new StatusResponseDto<ImageUploadResponseDto>
            {
                Status = "ok",
                Message = "Artist cover updated",
                Data = new()
                {
                    Url = new($"/images/music/{slug}.jpg", UriKind.Relative),
                    ColorPalette = artist.ColorPalette,
                },
            }
        );
    }
}
