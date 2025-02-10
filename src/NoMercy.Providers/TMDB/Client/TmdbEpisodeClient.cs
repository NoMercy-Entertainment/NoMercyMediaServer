using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Shared;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbEpisodeClient : TmdbBaseClient
{
    private readonly int _episodeNumber;
    private readonly int _seasonNumber;

    public TmdbEpisodeClient(int id, int seasonNumber, int episodeNumber, string[]? appendices = null) : base(id)
    {
        _seasonNumber = seasonNumber;
        _episodeNumber = episodeNumber;
    }

    public Task<TmdbEpisodeDetails?> Details()
    {
        return Get<TmdbEpisodeDetails>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber);
    }

    public Task<TmdbEpisodeAppends?> WithAppends(string[] appendices)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbEpisodeAppends>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber,
            queryParams);
    }

    public Task<TmdbEpisodeAppends?> WithAllAppends()
    {
        return WithAppends([
            "changes",
            "credits",
            "external_ids",
            "images",
            "translations",
            "videos"
        ]);
    }

    public Task<TmdbEpisodeChanges?> Changes(string startDate, string endDate)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Get<TmdbEpisodeChanges>(
            "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/changes",
            queryParams);
    }

    public Task<TmdbEpisodeCredits?> Credits()
    {
        return Get<TmdbEpisodeCredits>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber +
                                       "/credits");
    }

    public Task<TmdbEpisodeExternalIds?> ExternalIds()
    {
        return Get<TmdbEpisodeExternalIds>(
            "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/external_ids");
    }

    public Task<TmdbEpisodeImages?> Images()
    {
        return Get<TmdbEpisodeImages>(
            "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/images");
    }

    public Task<TmdbSharedTranslations?> Translations()
    {
        return Get<TmdbSharedTranslations>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber +
                                           "/translations");
    }

    public Task<Videos?> Videos()
    {
        return Get<Videos>("tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/videos");
    }
}