using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Models.Search;

public class TmdbMultiSearch : TmdbPaginatedResponse<(TmdbMovie, TmdbTvShow, TmdbPerson)>
{
}