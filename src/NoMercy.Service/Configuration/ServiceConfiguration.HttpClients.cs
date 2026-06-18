using NoMercy.NmSystem.Information;
using NoMercy.Providers.Helpers;

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    private static void ConfigureHttpClients(IServiceCollection services)
    {
        TimeSpan defaultTimeout = TimeSpan.FromMinutes(5);

        services.AddHttpClient(
            HttpClientNames.Tmdb,
            client =>
            {
                client.BaseAddress = new("https://api.themoviedb.org/3/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.TmdbImage,
            client =>
            {
                client.BaseAddress = new("https://image.tmdb.org/t/p/");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.Tvdb,
            client =>
            {
                client.BaseAddress = new("https://api4.thetvdb.com/v4/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.TvdbLogin,
            client =>
            {
                client.BaseAddress = new("https://api4.thetvdb.com/v4/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.MusicBrainz,
            client =>
            {
                client.BaseAddress = new("https://musicbrainz.org/ws/2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                // MusicBrainz puts anonymous UAs in a 50 req/s global shared
                // bucket — fine for our bursty low-traffic use. Deliberate
                // choice for privacy over the per-IP identified tier.
                client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
            }
        );

        services.AddHttpClient(
            HttpClientNames.AcoustId,
            client =>
            {
                client.BaseAddress = new("https://api.acoustid.org/v2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.OpenSubtitles,
            client =>
            {
                client.BaseAddress = new("https://api.opensubtitles.org/xml-rpc");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("text/xml"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.OpenSubtitlesDownload,
            client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.FanArt,
            client =>
            {
                client.BaseAddress = new("https://webservice.fanart.tv/v3/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.FanArtImage,
            client =>
            {
                client.BaseAddress = new("https://assets.fanart.tv");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
            }
        );

        services.AddHttpClient(
            HttpClientNames.CoverArt,
            client =>
            {
                client.BaseAddress = new("https://coverartarchive.org/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.CoverArtImage,
            client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
            }
        );

        services.AddHttpClient(
            HttpClientNames.Lrclib,
            client =>
            {
                client.BaseAddress = new("https://lrclib.net/api/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.MusixMatch,
            client =>
            {
                client.BaseAddress = new("https://apic-desktop.musixmatch.com/ws/1.1/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("authority", "apic-desktop.musixmatch.com");
                client.DefaultRequestHeaders.Add("cookie", "x-mxm-token-guid=");
            }
        );

        services.AddHttpClient(
            HttpClientNames.Tadb,
            client =>
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.NoMercyImage,
            client =>
            {
                client.BaseAddress = new("https://image.nomercy.tv/");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.KitsuIo,
            client =>
            {
                client.BaseAddress = new("https://kitsu.io/api/edge/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.General,
            client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );
    }
}
