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
using NoMercy.Providers.OpenSubtitles.Models;

namespace NoMercy.Providers.OpenSubtitles.Client;

public class OpenSubtitlesClient : OpenSubtitlesBaseClient
{
    public async Task<OpenSubtitlesClient> Login()
    {
        Login login = new()
        {
            MethodName = "LogIn",
            Params =
            [
                new() { Value = new() { String = "" } },
                new() { Value = new() { String = "" } },
                new() { Value = new() { String = "dut" } },
                new() { Value = new() { String = ExternalServicesConfig.Current.UserAgent } },
            ],
        };

        LoginResponse? x = await Post<Login, LoginResponse>("", login);
        AccessToken = x
            ?.Params?.Param?.Value?.Struct?.Member.FirstOrDefault(member => member.Name == "token")
            ?.Value?.String;

        return this;
    }

    /// <summary>
    /// Searches by OpenSubtitles moviehash + file size. Returns matching subtitle results.
    /// </summary>
    public async Task<SubtitleSearchResponse?> SearchSubtitlesByHash(
        string movieHash,
        long fileSize,
        string language
    )
    {
        SubtitleSearch searchRequest = new()
        {
            MethodCall = new()
            {
                MethodName = "SearchSubtitles",
                Params = new()
                {
                    Param =
                    [
                        new() { Value = new() { String = AccessToken! } },
                        new()
                        {
                            Value = new()
                            {
                                Array = new()
                                {
                                    Data = new()
                                    {
                                        Value = new()
                                        {
                                            Struct = new()
                                            {
                                                Member =
                                                [
                                                    new(
                                                        "sublanguageid",
                                                        new() { String = language }
                                                    ),
                                                    new("moviehash", new() { String = movieHash }),
                                                    new(
                                                        "moviebytesize",
                                                        new() { String = fileSize.ToString() }
                                                    ),
                                                ],
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    ],
                },
            },
        };

        return await Post<SubtitleSearch, SubtitleSearchResponse>("", searchRequest);
    }

    // TODO(subtitle-acquisition): TrustedUploadersOnly — client-side filter on SubFromTrusted field
    public async Task<SubtitleSearchResponse?> SearchSubtitles(string query, string language)
    {
        SubtitleSearch searchResponse = new()
        {
            MethodCall = new()
            {
                MethodName = "SearchSubtitles",
                Params = new()
                {
                    Param =
                    [
                        new() { Value = new() { String = AccessToken! } },
                        new()
                        {
                            Value = new()
                            {
                                Array = new()
                                {
                                    Data = new()
                                    {
                                        Value = new()
                                        {
                                            Struct = new()
                                            {
                                                Member =
                                                [
                                                    new(
                                                        "sublanguageid",
                                                        new() { String = language }
                                                    ),
                                                    new("query", new() { String = query }),
                                                ],
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    ],
                },
            },
        };

        return await Post<SubtitleSearch, SubtitleSearchResponse>("", searchResponse);
    }
}
