using AcoustID;
using NoMercy.Providers.FanArt.Models;
using NoMercy.Setup;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtMusicClient : FanArtBaseClient
{
    public FanArtMusicClient()
    {
        Configuration.ClientKey = ApiInfo.AcousticIdKey;
    }

    public Task<FanArtArtistDetails?> Artist(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtArtistDetails>("music/" + id, queryParams, priority);
    }

    public Task<FanArtAlbum?> Album(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtAlbum>("music/albums/" + id, queryParams, priority);
    }

    public Task<FanArtLabel?> Label(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtLabel>("music/labels/" + id, queryParams, priority);
    }

    public Task<FanArtLatest[]?> Latest(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtLatest[]>("music/latest", queryParams, priority);
    }
}