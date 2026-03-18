namespace NoMercy.Providers.Helpers;

public static class Url
{
    public static Uri ToHttps(this Uri url)
    {
        UriBuilder uriBuilder = new(url)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1 // default port for scheme
        };

        return uriBuilder.Uri;
    }
}