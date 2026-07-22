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
                predicate: member => member.Name.Equals(value: DataMember, comparisonType: StringComparison.OrdinalIgnoreCase)
            );

            if (data?.MemberValue.ArrayData.Values is null)
                continue;

            foreach (SubtitleSearchResponseMemberValue item in data.MemberValue.ArrayData.Values)
            {
                if (item.InnerStruct.Members.Count == 0)
                    continue;

                Dictionary<string, string> members = item
                    .InnerStruct.Members.Where(predicate: member => member.Name is not null)
                    .ToDictionary(
                        keySelector: member => member.Name,
                        elementSelector: member => member.MemberValue.StringValue ?? string.Empty,
                        comparer: StringComparer.OrdinalIgnoreCase
                    );

                string language = Coalesce(candidates: [members.GetValueOrDefault(key: "SubLanguageID"), members.GetValueOrDefault(key: "ISO639"), "und"]
                );

                yield return new(
                    Language: language,
                    SubRating: members.GetValueOrDefault(key: "SubRating"),
                    SubDownloadsCnt: members.GetValueOrDefault(key: "SubDownloadsCnt"),
                    SubFromTrusted: members.GetValueOrDefault(key: "SubFromTrusted"),
                    MovieFPS: members.GetValueOrDefault(key: "MovieFPS"),
                    SubDownloadLink: members.GetValueOrDefault(key: "SubDownloadLink"),
                    SubFormat: members.GetValueOrDefault(key: "SubFormat"),
                    MatchedBy: Coalesce(candidates: [members.GetValueOrDefault(key: "MatchedBy"), matchedBy]),
                    SubFileName: members.GetValueOrDefault(key: "SubFileName"),
                    MovieReleaseName: members.GetValueOrDefault(key: "MovieReleaseName"),
                    SubHearingImpaired: members.GetValueOrDefault(key: "SubHearingImpaired"),
                    UserNickName: members.GetValueOrDefault(key: "UserNickName")
                );
            }
        }
    }

    private static string Coalesce(params string?[] candidates)
    {
        return candidates.FirstOrDefault(predicate: value => !string.IsNullOrWhiteSpace(value: value))
            ?? string.Empty;
    }
}
