using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Extensions;

public static partial class Url
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

    public static string FileName(this Uri url)
    {
        return Path.GetFileName(url.LocalPath);
    }

    public static string BasePath(this Uri url)
    {
        return url.ToString().Replace("/" + url.FileName(), "");
    }

    public static bool HasSuccessStatus(this Uri url, string? contentType = null)
    {
        try
        {
            System.Net.Http.HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "NoMercy wMediaServer/0.1.0 ( admin@nomercy.tv )");

            if (contentType is not null)
                httpClient.DefaultRequestHeaders.Add("Accept", contentType);

            using HttpResponseMessage res = httpClient.SendAsync(new(HttpMethod.Head, url)).Result;
            return res.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public static string SafeHost(this string url)
    {
        url = ReplaceIpV4().Replace(url, "-");
        url = ReplaceIpV6().Replace(url, "-");
        return url;
    }

    [GeneratedRegex(":")]
    private static partial Regex ReplaceIpV6();
    [GeneratedRegex("\\.")]
    private static partial Regex ReplaceIpV4();
}