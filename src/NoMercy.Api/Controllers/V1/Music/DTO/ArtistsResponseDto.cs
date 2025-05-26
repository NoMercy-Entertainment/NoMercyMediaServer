using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record ArtistsResponseDto
{
    [JsonProperty("data")] public IEnumerable<ArtistsResponseItemDto> Data { get; set; } = [];

    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    public static readonly Func<MediaContext, Guid, string, IAsyncEnumerable<Artist>> GetArtists =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string letter) =>
            mediaContext.Artists
                .AsNoTracking()
                .Where(album => letter == "_"
                    ? Letters.Any(p => album.Name.StartsWith(p))
                    : album.Name.StartsWith(letter)
                )
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .Include(artist => artist.Images
                    .Where(image => image.Type == "thumb")
                    .OrderByDescending(image => image.VoteCount)
                )
                .GroupBy(artist => artist.Name).Select(x => x.First())
        );
}