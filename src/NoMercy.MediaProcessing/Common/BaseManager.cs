using NoMercy.NmSystem.Extensions;

namespace NoMercy.MediaProcessing.Common;

public class BaseManager : IBaseManager
{
    public string BaseUrl(string title, DateTime? releaseDate)
    {
        return "/" + string
            .Concat(title, ".(", releaseDate.ParseYear(), ")")
            .CleanFileName();
    }

    public string BaseUrl(string name)
    {
        return string
            .Concat(name[0], "/", name)
            .CleanFileName();
    }
}