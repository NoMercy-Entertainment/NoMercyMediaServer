using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Shared;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbEpisodeClient : TmdbBaseClient
{
    private readonly int _episodeNumber;
    private readonly int _seasonNumber;

    public TmdbEpisodeClient(int id, int seasonNumber, int episodeNumber, string[]? appendices = null, string? language = "en-US") : base(id, language!)
    {
        _seasonNumber = seasonNumber;
        _episodeNumber = episodeNumber;
    }

    public Task<TmdbEpisodeDetails?> Details(bool? priority = false)
    {
        return Get<TmdbEpisodeDetails>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber,
            priority: priority);
    }

    public Task<TmdbEpisodeAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbEpisodeAppends>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber,
            queryParams, priority: priority);
    }

    public Task<TmdbEpisodeAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "changes",
            "credits",
            "external_ids",
            "images",
            "translations",
            "videos"
        ], priority: priority);
    }

    public Task<TmdbEpisodeChanges?> Changes(string startDate, string endDate, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Get<TmdbEpisodeChanges>(
            "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/changes",
            queryParams, priority: priority);
    }

    public Task<TmdbEpisodeCredits?> Credits(bool? priority = false)
    {
        return Get<TmdbEpisodeCredits>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber +
                                       "/credits", priority: priority);
    }

    public Task<TmdbEpisodeExternalIds?> ExternalIds(bool? priority = false)
    {
        return Get<TmdbEpisodeExternalIds>(
            "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/external_ids",
            priority: priority);
    }

    public Task<TmdbEpisodeImages?> Images(bool? priority = false)
    {
        return Get<TmdbEpisodeImages>(
            "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/images", priority: priority);
    }

    public Task<TmdbSharedTranslations?> Translations(bool? priority = false)
    {
        return Get<TmdbSharedTranslations>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber +
                                           "/translations", priority: priority);
    }

    public Task<Videos?> Videos(bool? priority = false)
    {
        return Get<Videos>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/videos",
            priority: priority);
    }
}