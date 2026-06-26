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
using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Information;

public static class Config
{
    private const string DefaultAuthBaseUrl = "https://auth.nomercy.tv/realms/NoMercyTV/";
    private const string DefaultAppBaseUrl = "https://app.nomercy.tv/";
    private const string DefaultApiBaseUrl = "https://api.nomercy.tv/";

    public static string AuthBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_AUTH_URL") ?? DefaultAuthBaseUrl;

    public static readonly string TokenClientId = "nomercy-server";

    public static string AppBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_APP_URL") ?? DefaultAppBaseUrl;

    public static string ApiBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_API_URL") ?? DefaultApiBaseUrl;

    public static string ApiServerBaseUrl { get; set; } = $"{ApiBaseUrl}v1/server/";

    public static string UserAgent =>
        $"NoMercy MediaServer/{Software.Version} ( admin@nomercy.tv )";

    public static string ManagementPipeName
    {
        get => field ?? "NoMercyManagement";
        set;
    }

    public static string ManagementSocketPath =>
        Path.Combine(AppFiles.AppPath, "nomercy-management.sock");

    public static bool Swagger
    {
        get => RuntimeServerSettings.Current.Swagger;
        set => RuntimeServerSettings.Current.Swagger = value;
    }

    public static bool IsDev { get; set; }
    public static bool IsTest { get; set; }

    public static bool? AllowAdultContent
    {
        get => RuntimeServerSettings.Current.AllowAdultContent;
        set => RuntimeServerSettings.Current.AllowAdultContent = value;
    }

    // Safe-by-default: adult content is shown only when explicitly enabled.
    // A null (never configured) or false setting both resolve to hidden.
    public static bool ShowAdultContent => AllowAdultContent == true;
}
