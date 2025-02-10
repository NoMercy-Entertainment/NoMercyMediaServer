using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbSeasonClient : TmdbBaseClient, IDisposable
{
    private readonly int _seasonNumber;

    public TmdbSeasonClient(int tvId, int seasonNumber, string[]? appendices = null) : base(tvId)
    {
        _seasonNumber = seasonNumber;
    }

    public TmdbEpisodeClient Episode(int episodeNumber, string[]? items = null)
    {
        return new TmdbEpisodeClient(Id, _seasonNumber, episodeNumber);
    }

    public Task<TmdbSeasonDetails?> Details()
    {
        return Get<TmdbSeasonDetails>("tv/" + Id + "/season/" + _seasonNumber);
    }

    public Task<TmdbSeasonAppends?> WithAppends(string[] appendices)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbSeasonAppends>("tv/" + Id + "/season/" + _seasonNumber, queryParams);
    }

    public Task<TmdbSeasonAppends?> WithAllAppends()
    {
        return WithAppends([
            "aggregate_credits",
            "changes",
            "credits",
            "external_ids",
            "images",
            "translations"
        ]);
    }

    //public Task<AccountStates?> AccountStates()
    //{
    //    strreturn Get<Details>(("tv/" + Id + "/season/" + SeasonNumber + "/account_states");
    //    
    //}

    public Task<TmdbSeasonAggregatedCredits?> AggregatedCredits()
    {
        return Get<TmdbSeasonAggregatedCredits>("tv/" + Id + "/season/" + _seasonNumber + "/aggregate_credits");
    }

    public Task<TmdbSeasonChanges?> Changes(string startDate, string endDate)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Get<TmdbSeasonChanges>("tv/" + Id + "/season/" + _seasonNumber + "/changes", queryParams);
    }

    public Task<TmdbSeasonCredits?> Credits()
    {
        return Get<TmdbSeasonCredits>("tv/" + Id + "/season/" + _seasonNumber + "/credits");
    }

    public Task<TmdbSeasonExternalIds?> ExternalIds()
    {
        return Get<TmdbSeasonExternalIds>("tv/" + Id + "/season/" + _seasonNumber + "/external_ids");
    }

    public Task<TmdbSeasonImages?> Images()
    {
        return Get<TmdbSeasonImages>("tv/" + Id + "/season/" + _seasonNumber + "/images");
    }

    public Task<TmdbSharedTranslations?> Translations()
    {
        return Get<TmdbSharedTranslations>("tv/" + Id + "/season/" + _seasonNumber + "/translations");
    }

    public Task<TmdbSeasonVideos?> Videos()
    {
        return Get<TmdbSeasonVideos>("tv/" + Id + "/season/" + _seasonNumber + "/videos");
    }

    public new void Dispose()
    {
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }
}