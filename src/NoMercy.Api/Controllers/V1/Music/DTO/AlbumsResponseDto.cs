using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record AlbumsResponseDto
{
    [JsonProperty("data")] public IEnumerable<AlbumsResponseItemDto> Data { get; set; } = [];

    private static readonly string[] Numbers = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    public static readonly Func<MediaContext, Guid, string, IAsyncEnumerable<Album>> GetAlbums =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string letter = "_") => mediaContext.Albums
            .AsNoTracking()
            .OrderBy(album => album.Name)
            .Where(album => letter == "_"
                ? Numbers.Any(p => album.Name.StartsWith(p))
                : album.Name.StartsWith(letter)
            )
            .Include(album => album.Translations)
            .GroupBy(artist => artist.Name).Select(x => x.First())
        );
}