// ReSharper disable All

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzGenreClient : MusicBrainzBaseClient
{
    public MusicBrainzGenreClient() : base()
    {
    }

    public async Task<List<MusicBrainzGenre>> All(int page = 1)
    {
        List<MusicBrainzGenre> genres = [];

        MusicBrainzAllGenres? data = await Get<MusicBrainzAllGenres>("genre/all", new Dictionary<string, string>
        {
            ["limit"] = 100.ToString(),
            ["offset"] = (page * 100).ToString(),
            ["fmt"] = "json"
        });

        if (data is null) return genres;

        genres.AddRange(data.Genres);

        for (int i = 0; i < data.GenreCount / data.Genres.Length; i++)
        {
            MusicBrainzAllGenres? data2 = await Get<MusicBrainzAllGenres>("genre/all", new Dictionary<string, string>
            {
                ["limit"] = data.Genres.Length.ToString(),
                ["offset"] = (i * data.Genres.Length).ToString()
            });

            if (data2 is null) continue;

            genres.AddRange(data2.Genres);
        }

        return genres;
    }

    public async Task<MusicBrainzGenre?> SearchGenre(string query)
    {
        MusicBrainzAllGenres? data = await Get<MusicBrainzAllGenres>("genre", new Dictionary<string, string>
        {
            ["query"] = query,
            ["fmt"] = "json"
        });

        return data?.Genres.FirstOrDefault();
    }
}