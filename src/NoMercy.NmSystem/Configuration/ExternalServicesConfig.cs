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

using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.Configuration;

/// <summary>
/// Endpoints and identifiers for NoMercy's external services (auth, app, API).
/// Values resolve from the NOMERCY_*_URL environment variables at startup,
/// falling back to production defaults. <see cref="Current"/> is the ambient
/// instance used across the codebase; the type is also bound for IOptions.
/// </summary>
public class ExternalServicesConfig
{
    private const string DefaultAuthBaseUrl = "https://auth.nomercy.tv/realms/NoMercyTV/";
    private const string DefaultAppBaseUrl = "https://app.nomercy.tv/";
    private const string DefaultApiBaseUrl = "https://api.nomercy.tv/";

    public string AuthBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_AUTH_URL") ?? DefaultAuthBaseUrl;

    public string AppBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_APP_URL") ?? DefaultAppBaseUrl;

    public string ApiBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_API_URL") ?? DefaultApiBaseUrl;

    public string ApiServerBaseUrl { get; set; }

    public string UserAgent => $"NoMercy MediaServer/{Software.Version} ( admin@nomercy.tv )";

    /// <summary>
    /// OpenSubtitles answers an unregistered user agent with a 102-byte "Become VIP member"
    /// placeholder in place of the cue file — a 200 OK, correctly gzipped, and useless. Only a
    /// user agent registered with them returns real subtitles, so the general
    /// <see cref="UserAgent"/> cannot be used here. Replace this with a NoMercy-registered agent
    /// once opensubtitles.org grants one; nothing else about the client needs to change.
    /// </summary>
    public string OpenSubtitlesUserAgent { get; set; } = "VLSub 0.11.1";

    public string TokenClientId { get; set; } = "nomercy-server";

    public ExternalServicesConfig()
    {
        ApiServerBaseUrl = $"{ApiBaseUrl}v1/server/";
    }

    public static ExternalServicesConfig Current { get; } = new();
}
