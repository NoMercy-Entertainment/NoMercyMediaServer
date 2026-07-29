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

using NoMercy.NmSystem.Configuration;

namespace NoMercy.Notifications.Push;

/// <summary>
/// Turns a TMDB-style relative image path (e.g. "/abc123.jpg", as stored on
/// Movie/Tv/Episode) into an absolute URL a phone can fetch directly. Routes
/// through the same NoMercy CDN proxy the web and KMP clients already build
/// client-side (app.nomercy.tv/tmdb-images?width=...) instead of
/// image.tmdb.org, so a push notification gets the identical pre-sized asset
/// those clients would have rendered. Reuses ExternalServicesConfig.AppBaseUrl
/// rather than hardcoding the host, so a NOMERCY_APP_URL override (dev/self-hosted
/// relay) still resolves to a phone-reachable address.
/// </summary>
public static class PushArtworkUrl
{
    public const int BackdropWidth = 500;
    public const int PosterWidth = 200;

    public static string? Build(string? path, int width)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string baseUrl = ExternalServicesConfig.Current.AppBaseUrl.TrimEnd('/');
        return $"{baseUrl}/tmdb-images{path}?width={width}";
    }
}
