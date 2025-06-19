using AniDB;
using AniDB.ResponseItems;

namespace NoMercy.Providers.AniDb.Models;

public class AniDbCallbackObject<T>(Action<T> callback) : IAniDBMessageResponseCallback<T>
    where T : IAniDBResponseItem
{
    public void Callback(T messageItem)
    {
        callback(messageItem);
    }
}