using AcoustID;
using NoMercy.Providers.FanArt.Models;
using NoMercy.Setup.Server;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtMovieClient : FanArtBaseClient
{
    public FanArtMovieClient()
    {
        Configuration.ClientKey = ApiKeyStore.Current.AcousticIdKey;
    }

    public Task<FanArtMovie?> Movie(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtMovie>("movies/" + id, queryParams, priority);
    }

    public Task<FanArtLatest[]?> Latest(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtLatest[]>("movies/latest" + id, queryParams, priority);
    }
}
