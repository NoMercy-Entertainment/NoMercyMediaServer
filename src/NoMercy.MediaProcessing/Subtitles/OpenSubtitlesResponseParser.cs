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

using NoMercy.Encoder.Subtitles;
using NoMercy.Providers.OpenSubtitles.Models;

namespace NoMercy.MediaProcessing.Subtitles;

/// <summary>
/// Flattens the XML-RPC SearchSubtitles envelope into normalized results.
/// </summary>
public static class OpenSubtitlesResponseParser
{
    private const string DataMember = "data";

    public static IEnumerable<OpenSubtitlesSearchResult> Parse(
        SubtitleSearchResponse? response,
        string matchedBy
    )
    {
        if (response?.Params is null)
            yield break;

        foreach (SubtitleSearchResponseParam param in response.Params)
        {
            // The envelope is {status, data, seconds} — every result lives in the "data" member's
            // array, never on the param's own value. Reading the param level finds an empty list
            // and reports "no subtitles found" over a response full of them.
            SubtitleSearchResponseMember? data = param.Value.InnerStruct.Members.FirstOrDefault(
                member => member.Name.Equals(DataMember, StringComparison.OrdinalIgnoreCase)
            );

            if (data?.MemberValue.ArrayData.Values is null)
                continue;

            foreach (SubtitleSearchResponseMemberValue item in data.MemberValue.ArrayData.Values)
            {
                if (item.InnerStruct.Members.Count == 0)
                    continue;

                Dictionary<string, string> members = item
                    .InnerStruct.Members.Where(member => member.Name is not null)
                    .ToDictionary(
                        member => member.Name,
                        member => member.MemberValue.StringValue ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase
                    );

                string language = Coalesce([members.GetValueOrDefault("SubLanguageID"), members.GetValueOrDefault("ISO639"), "und"]
                );

                yield return new(
                    language,
                    members.GetValueOrDefault("SubRating"),
                    members.GetValueOrDefault("SubDownloadsCnt"),
                    members.GetValueOrDefault("SubFromTrusted"),
                    members.GetValueOrDefault("MovieFPS"),
                    members.GetValueOrDefault("SubDownloadLink"),
                    members.GetValueOrDefault("SubFormat"),
                    Coalesce([members.GetValueOrDefault("MatchedBy"), matchedBy]),
                    members.GetValueOrDefault("SubFileName"),
                    members.GetValueOrDefault("MovieReleaseName"),
                    members.GetValueOrDefault("SubHearingImpaired"),
                    members.GetValueOrDefault("UserNickName")
                );
            }
        }
    }

    private static string Coalesce(params string?[] candidates)
    {
        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;
    }
}
