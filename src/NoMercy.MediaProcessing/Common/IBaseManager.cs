namespace NoMercy.MediaProcessing.Common;

public interface IBaseManager
{
    string BaseUrl(string title, DateTime? releaseDate);
    string BaseUrl(string name);
}