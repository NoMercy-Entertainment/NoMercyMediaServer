
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CollectionId), nameof(MovieId))]
[Index(nameof(CollectionId))]
[Index(nameof(MovieId))]
public class CollectionMovie
{
    [JsonProperty("collection_id")] public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    [JsonProperty("movie_id")] public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public CollectionMovie()
    {
    }

    public CollectionMovie(int collectionId, int movieId)
    {
        CollectionId = collectionId;
        MovieId = movieId;
    }

    // public CollectionMovie(Providers.TMDB.Models.Movies.TmdbMovie collectionId, int collectionsId)
    // {
    //     MovieId = collectionId.Id;
    //     CollectionId = collectionsId;
    // }
}
