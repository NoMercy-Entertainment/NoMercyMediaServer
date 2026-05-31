namespace NoMercy.Providers.TVDB.Client;

/// <summary>
/// Alias for <see cref="TvdbSeriesClient"/> — TVDB calls TV shows "series" but
/// callers used to NoMercy's "Tv" naming may reach for TvdbTvClient out of habit.
/// </summary>
public class TvdbTvClient : TvdbSeriesClient
{
    public TvdbTvClient(int id = 0, string language = "eng")
        : base(id, language) { }
}
