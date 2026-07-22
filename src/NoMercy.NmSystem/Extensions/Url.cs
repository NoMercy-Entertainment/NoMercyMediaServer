// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Extensions;

public static partial class Url
{
    public static Uri ToHttps(this Uri url)
    {
        UriBuilder uriBuilder = new(uri: url)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1, // default port for scheme
        };

        return uriBuilder.Uri;
    }

    public static string FileName(this Uri url)
    {
        return Path.GetFileName(path: url.LocalPath);
    }

    public static string BasePath(this Uri url)
    {
        return url.ToString().Replace(oldValue: "/" + url.FileName(), newValue: "");
    }

    public static bool HasSuccessStatus(this Uri url, string? contentType = null)
    {
        try
        {
            HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add(
                name: "User-Agent",
                value: "NoMercy wMediaServer/0.1.0 ( admin@nomercy.tv )"
            );

            if (contentType is not null)
                httpClient.DefaultRequestHeaders.Add(name: "Accept", value: contentType);

            using HttpResponseMessage res = httpClient.SendAsync(request: new(method: HttpMethod.Head, requestUri: url)).Result;
            return res.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string SafeHost(this string url)
    {
        url = ReplaceIpV4().Replace(input: url, replacement: "-");
        url = ReplaceIpV6().Replace(input: url, replacement: "-");
        return url;
    }

    [GeneratedRegex(pattern: ":")]
    private static partial Regex ReplaceIpV6();

    [GeneratedRegex(pattern: "\\.")]
    private static partial Regex ReplaceIpV4();
}
