using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbSeasonClient : TmdbBaseClient, IDisposable
{
    private readonly int _seasonNumber;

    public TmdbSeasonClient(int tvId, int seasonNumber, string[]? appendices = null, string? language = "en-US") : base(tvId, language!)
    {
        _seasonNumber = seasonNumber;
    }

    public TmdbEpisodeClient Episode(int episodeNumber, string[]? items = null)
    {
        return new TmdbEpisodeClient(Id, _seasonNumber, episodeNumber);
    }

    public Task<TmdbSeasonDetails?> Details(bool? priority = false)
    {
        return Get<TmdbSeasonDetails>("tv/" + Id + "/season/" + _seasonNumber, priority: priority);
    }

    public Task<TmdbSeasonAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbSeasonAppends>("tv/" + Id + "/season/" + _seasonNumber, queryParams, priority: priority);
    }

    public Task<TmdbSeasonAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "aggregate_credits",
            "changes",
            "credits",
            "external_ids",
            "images",
            "translations"
        ], priority);
    }

    //public Task<AccountStates?> AccountStates()
    //{
    //    strreturn Get<Details>(("tv/" + Id + "/season/" + SeasonNumber + "/account_states");
    //    
    //}

    public Task<TmdbSeasonAggregatedCredits?> AggregatedCredits(bool? priority = false)
    {
        return Get<TmdbSeasonAggregatedCredits>("tv/" + Id + "/season/" + _seasonNumber + "/aggregate_credits",
            priority: priority);
    }

    public async Task<TmdbSeasonChanges?> Changes(string startDate, string endDate, bool? priority = false)
    {
        // First get the season details to obtain the season ID
        var seasonDetails = await Details(priority);
        if (seasonDetails == null) return null;
        
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        // Use the season ID for the changes endpoint
        return await Get<TmdbSeasonChanges>("tv/season/" + seasonDetails.Id + "/changes", queryParams,
            priority: priority);
    }

    public Task<TmdbSeasonCredits?> Credits(bool? priority = false)
    {
        return Get<TmdbSeasonCredits>("tv/" + Id + "/season/" + _seasonNumber + "/credits", priority: priority);
    }

    public Task<TmdbSeasonExternalIds?> ExternalIds(bool? priority = false)
    {
        return Get<TmdbSeasonExternalIds>("tv/" + Id + "/season/" + _seasonNumber + "/external_ids",
            priority: priority);
    }

    public Task<TmdbSeasonImages?> Images(bool? priority = false)
    {
        return Get<TmdbSeasonImages>("tv/" + Id + "/season/" + _seasonNumber + "/images", priority: priority);
    }

    public Task<TmdbSharedTranslations?> Translations(bool? priority = false)
    {
        return Get<TmdbSharedTranslations>("tv/" + Id + "/season/" + _seasonNumber + "/translations",
            priority: priority);
    }

    public Task<TmdbSeasonVideos?> Videos(bool? priority = false)
    {
        return Get<TmdbSeasonVideos>("tv/" + Id + "/season/" + _seasonNumber + "/videos", priority: priority);
    }

    public new void Dispose()
    {
        base.Dispose();
    }
}