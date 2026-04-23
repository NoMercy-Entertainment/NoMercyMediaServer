// ReSharper disable All

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzGenreClient : MusicBrainzBaseClient
{
    public MusicBrainzGenreClient()
        : base() { }

    public async Task<List<MusicBrainzGenre>> All()
    {
        List<MusicBrainzGenre> genres = [];

        MusicBrainzAllGenres? firstPage = await Get<MusicBrainzAllGenres>(
            "genre/all",
            new Dictionary<string, string>
            {
                ["limit"] = "100",
                ["offset"] = "0",
                ["fmt"] = "json",
            }
        );

        if (firstPage is null)
            return genres;

        genres.AddRange(firstPage.Genres);

        int pageSize = firstPage.Genres.Length;

        if (pageSize == 0)
            return genres;

        for (long offset = pageSize; offset < firstPage.GenreCount; offset += pageSize)
        {
            MusicBrainzAllGenres? page = await Get<MusicBrainzAllGenres>(
                "genre/all",
                new Dictionary<string, string>
                {
                    ["limit"] = pageSize.ToString(),
                    ["offset"] = offset.ToString(),
                    ["fmt"] = "json",
                }
            );

            if (page is null)
                continue;

            genres.AddRange(page.Genres);
        }

        return genres;
    }

    public Task<MusicBrainzAllGenres?> Probe() =>
        Get<MusicBrainzAllGenres>(
            "genre/all",
            new Dictionary<string, string>
            {
                ["limit"] = "1",
                ["offset"] = "0",
                ["fmt"] = "json",
            }
        );

    public async Task<MusicBrainzGenre?> SearchGenre(string query)
    {
        MusicBrainzAllGenres? data = await Get<MusicBrainzAllGenres>(
            "genre",
            new Dictionary<string, string> { ["query"] = query, ["fmt"] = "json" }
        );

        return data?.Genres.FirstOrDefault();
    }
}
