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

namespace NoMercy.Setup.Server;

public class ApiKeyStore : IApiKeyStore
{
    private static IApiKeyStore? _instance;

    // Lazily create a default (empty-keys) store if nothing has constructed one yet, so
    // early access and the separate Launcher process never crash. The DI singleton replaces
    // this during boot (its ctor sets _instance) and ApiKeyLoader then fills in the keys.
    public static IApiKeyStore Current => _instance ??= new ApiKeyStore();

    public ApiKeyStore()
    {
        _instance = this;
    }

    public string AcousticIdKey { get; internal set; } = string.Empty;
    public string FanArtApiKey { get; internal set; } = string.Empty;
    public string FanArtClientKey { get; internal set; } = string.Empty;
    public string JwplayerKey { get; internal set; } = string.Empty;
    public string MakeMkvKey { get; internal set; } = string.Empty;
    public string MusixmatchKey { get; internal set; } = string.Empty;
    public string OmdbKey { get; internal set; } = string.Empty;
    public string RottenTomatoes { get; internal set; } = string.Empty;
    public string TadbKey { get; internal set; } = string.Empty;
    public string TmdbKey { get; internal set; } = string.Empty;
    public string TmdbToken { get; internal set; } = string.Empty;
    public string TvdbKey { get; internal set; } = string.Empty;
    public bool KeysLoaded { get; internal set; }

    public string[] Colors { get; } =
    [
        "#CBAFFF",
        "#B5A0FF",
        "#9F91FF",
        "#8982FF",
        "#7373FF",
        "#5D64FF",
        "#4755FF",
        "#3146FF",
        "#1B37FF",
        "#0528FF",
    ];

    public string Quote { get; } = GetRandomQuote();

    private static string GetRandomQuote()
    {
        string[] quotes =
        [
            "NoMercy is the best media server",
            "NoMercy is the future of media servers",
            "NoMercy is the most advanced media server",
            "NoMercy is the most powerful media server",
            "NoMercy is the most reliable media server",
            "NoMercy is the most secure media server",
            "NoMercy is the most stable media server",
            "NoMercy is the most user-friendly media server",
            "NoMercy is the most versatile media server",
            "NoMercy is the only media server you'll ever need",
        ];

        return quotes[new Random().Next(maxValue: quotes.Length)];
    }
}
