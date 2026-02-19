using NoMercy.Providers.TMDB.Models.Configuration;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbConfigClient : TmdbBaseClient
{
    public Task<TmdbConfiguration?> Configuration()
    {
        return Get<TmdbConfiguration>("configuration");
    }

    public Task<List<TmdbLanguage>?> Languages()
    {
        return Get<List<TmdbLanguage>>("configuration/languages");
    }

    public Task<List<TmdbCountry>?> Countries()
    {
        return Get<List<TmdbCountry>>("configuration/countries");
    }

    public Task<List<TmdbJob>?> Jobs()
    {
        return Get<List<TmdbJob>>("configuration/jobs");
    }

    public Task<List<string>?> PrimaryTranslations()
    {
        return Get<List<string>>("configuration/primary_translations");
    }

    public Task<List<TmdbTimezone>?> Timezones()
    {
        return Get<List<TmdbTimezone>>("configuration/timezones");
    }
}