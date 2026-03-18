using NoMercy.Providers.Tadb.Models;

namespace NoMercy.Providers.Tadb.Client;

public class TadbArtistClient : TadbBaseClient
{
    public TadbArtist? ByMusicBrainzId(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            { "i", id.ToString() }
        };

        try
        {
            return Get<TadbArtistResponse>("artist-mb.php", queryParams, priority)
                .Result?.Artists?.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }
}