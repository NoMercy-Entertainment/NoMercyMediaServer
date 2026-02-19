using AniDB;
using AniDB.RequestEnums;
using AniDB.ResponseItems;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.AniDb.Models;

namespace NoMercy.Providers.AniDb.Clients;

public static class AniDbRandomAnime
{
    public static Task<AniDBAnimeItem> GetRandomAnime()
    {
        AniDBClient client = AniDbBaseClient.Client();
        TaskCompletionSource<AniDBAnimeItem> tcs = new();

        client.FetchRandomAnime((response) =>
        {
            Logger.AniDb(response.StatusCode.ToString());
            Logger.AniDb(response.StatusMessage);

            response.GetMessageItem(0, new AniDbCallbackObject<AniDBAnimeItem>(messageItem =>
                {
                    messageItem.parseContentsDefault();

                    tcs.SetResult(messageItem);
                })
            );
        }, RandomAnimeSource.ANY, 2);

        return tcs.Task;
    }
}