using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbSearchClient : TmdbBaseClient
{
    public Task<TmdbPaginatedResponse<TmdbMovie>?> Movie(string query, string? year)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["query"] = query,
            ["primary_release_year"] = year ?? "",
            ["include_adult"] = "false"
        };

        return Get<TmdbPaginatedResponse<TmdbMovie>>("search/movie", queryParams);
    }

    public Task<TmdbPaginatedResponse<TmdbTvShow>?> TvShow(string query, string? year)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["query"] = query,
            ["first_air_date_year"] = year ?? "",
            ["include_adult"] = "false"
        };

        return Get<TmdbPaginatedResponse<TmdbTvShow>>("search/tv", queryParams);
    }

    public Task<TmdbPaginatedResponse<TmdbPerson>?> Person(string query, string? year)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["query"] = query,
            ["primary_release_year"] = year ?? "",
            ["include_adult"] = "false"
        };

        return Get<TmdbPaginatedResponse<TmdbPerson>>("search/person", queryParams);
    }

    public Task<TmdbPaginatedResponse<TmdbMultiSearch>?> Multi(string query, string? year)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["query"] = query,
            ["primary_release_year"] = year ?? "",
            ["include_adult"] = "false"
        };

        return Get<TmdbPaginatedResponse<TmdbMultiSearch>>("search/multi", queryParams);
    }

    public Task<TmdbPaginatedResponse<TmdbCollection>?> Collection(string query, string? year)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["query"] = query,
            ["primary_release_year"] = year ?? "",
            ["include_adult"] = "false"
        };

        return Get<TmdbPaginatedResponse<TmdbCollection>>("search/collection", queryParams);
    }

    public Task<TmdbPaginatedResponse<TmdbNetwork>?> Network(string query, string? year)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["query"] = query,
            ["primary_release_year"] = year ?? "",
            ["include_adult"] = "false"
        };

        return Get<TmdbPaginatedResponse<TmdbNetwork>>("search/network", queryParams);
    }

    public Task<TmdbPaginatedResponse<TmdbKeyword>?> Keyword(string query, string? year)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["query"] = query,
            ["primary_release_year"] = year ?? "",
            ["include_adult"] = "false"
        };

        return Get<TmdbPaginatedResponse<TmdbKeyword>>("search/keyword", queryParams);
    }
}